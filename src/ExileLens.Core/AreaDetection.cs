using System.Text;
using System.Text.RegularExpressions;

namespace ExileLens.Core;

public sealed record AreaEntry(string Id, int Level, string Seed)
{
    public bool IsLogbook => Id.StartsWith("ExpeditionLogBook_", StringComparison.Ordinal);
    public bool IsExpedition => IsLogbook || Id == "ExpeditionLeagueBoss";
    public string DisplayName => Id switch
    {
        "ExpeditionLeagueBoss" => "Expedition boss",
        _ when IsLogbook => Id["ExpeditionLogBook_".Length..] + " Logbook",
        _ when Id.StartsWith("Hideout", StringComparison.Ordinal) => "Hideout",
        _ => Id
    };
}

public static partial class AreaParser
{
    // Match the structured debug record, never player chat or asset warnings.
    [GeneratedRegex("^\\d{4}/\\d{2}/\\d{2} \\d{2}:\\d{2}:\\d{2} \\d+ [0-9a-fA-F]+ \\[DEBUG Client \\d+\\] Generating level (?<level>\\d+) area \"(?<id>[^\"]+)\" with seed (?<seed>\\d+)\\r?$")]
    private static partial Regex Pattern();

    public static AreaEntry? Parse(string line)
    {
        var match = Pattern().Match(line);
        return match.Success && int.TryParse(match.Groups["level"].Value, out var level)
            ? new(match.Groups["id"].Value, level, match.Groups["seed"].Value) : null;
    }
}

/// <summary>Incremental UTF-8 log reader. Starts at EOF so old sessions never activate the overlay.</summary>
public sealed class LogTail
{
    private readonly string path;
    private long offset;
    private bool initialized;
    private DateTime creation;
    private readonly StringBuilder pending = new();
    private Decoder decoder = Encoding.UTF8.GetDecoder();
    public LogTail(string path) => this.path = path;

    public IReadOnlyList<AreaEntry> Poll()
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var created = File.GetCreationTimeUtc(path);
        if (!initialized)
        {
            offset = stream.Length;
            creation = created;
            initialized = true;
            return Array.Empty<AreaEntry>();
        }
        if (stream.Length < offset || created != creation)
        {
            offset = 0;
            pending.Clear();
            decoder = Encoding.UTF8.GetDecoder();
            creation = created;
        }
        stream.Position = offset;
        var entries = new List<AreaEntry>();
        var bytes = new byte[65536];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        // Bound each poll so a busy log never monopolises the UI thread.
        var remaining = Math.Min(stream.Length - offset, 1024 * 1024);
        while (remaining > 0)
        {
            int count = stream.Read(bytes, 0, (int)Math.Min(bytes.Length, remaining));
            if (count == 0) break;
            offset += count;
            remaining -= count;
            int charCount = decoder.GetChars(bytes, 0, count, chars, 0);
            for (int i = 0; i < charCount; i++)
            {
                if (chars[i] == '\n')
                {
                    if (AreaParser.Parse(pending.ToString()) is { } area) entries.Add(area);
                    pending.Clear();
                }
                else if (pending.Length < 65536) pending.Append(chars[i]);
            }
        }
        return entries;
    }
}
