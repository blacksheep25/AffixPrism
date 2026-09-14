using System.Text.Json;

namespace AffixPrism.Core;

public sealed record LeagueOption(string Id, string Name);
public static class Leagues
{
    // Current economy leagues shown by poe.ninja on 2026-09-09. Offline fallback,
    // never described as live data. API responses replace this list when available.
    public static IReadOnlyList<LeagueOption> Bundled => new[]
    {
        new LeagueOption("Forbidden Rites", "Forbidden Rites"),
        new LeagueOption("Runes of Aldur", "Runes of Aldur"),
        new LeagueOption("HC Forbidden Rites", "HC Forbidden Rites"),
        new LeagueOption("HC Runes of Aldur", "HC Runes of Aldur"),
        new LeagueOption("Standard", "Standard"), new LeagueOption("Hardcore", "Hardcore")
    };
    public static IReadOnlyList<LeagueOption> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("Expected league list");
        var result = new List<LeagueOption>();
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String) continue;
            string value = id.GetString()!.Trim();
            if (value.Length is 0 or > 100 || value.Any(char.IsControl)) continue;
            string name = entry.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : value;
            if (result.All(x => x.Id != value)) result.Add(new(value, string.IsNullOrWhiteSpace(name) ? value : name));
        }
        if (result.Count == 0) throw new JsonException("Empty league list");
        return result;
    }
    public static IReadOnlyList<LeagueOption> WithSaved(IReadOnlyList<LeagueOption> options, string saved)
        => string.IsNullOrWhiteSpace(saved) || options.Any(x => x.Id == saved) ? options : options.Append(new LeagueOption(saved, saved + " (saved league)")).ToArray();
}
