using System.Text.RegularExpressions;

namespace ExileLens.Core;

public static class BaseTypes
{
    private static readonly Lazy<string[]> Names = new(() =>
    {
        using var stream = typeof(BaseTypes).Assembly.GetManifestResourceStream("ExileLens.Core.Data.base-types.txt")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    });
    public static bool IsKnown(string name) => Names.Value.Contains(name, StringComparer.OrdinalIgnoreCase);
    public static string? ResolveMagicName(string name)
    {
        // Match actual bases, not a guessed number of prefix/suffix words.
        var matches = Names.Value.Where(b => Regex.IsMatch(name, @"(?:^|\s)" + Regex.Escape(b) + @"(?:$|\s+of\s)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).ToArray();
        // Nested names such as an advanced base must win over their shorter base.
        var longest = matches.Where(b => !matches.Any(other => other.Length > b.Length && other.Contains(b,StringComparison.OrdinalIgnoreCase))).ToArray();
        return longest.Length == 1 ? longest[0] : null;
    }
}
