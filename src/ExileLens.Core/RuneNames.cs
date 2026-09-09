using System.Text.RegularExpressions;

namespace ExileLens.Core;

public sealed record RuneNameMatch(EconomyRow Row, bool Approximate, int Quantity = 1)
{
    public string PriceLabel => Quantity == 1 ? Row.PriceLabel : $"{Quantity} × {Row.PriceLabel} = {Quantity * Row.Value:0.##} {Row.Currency}";
}
public static class RuneNames
{
    private static string Normal(string value) => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]", "");
    private static string Tier(string value) => new[] { "lesser", "greater", "perfect" }.FirstOrDefault(x => Normal(value).StartsWith(x, StringComparison.Ordinal)) ?? "standard";
    public static RuneNameMatch? Match(string text, float confidence, IReadOnlyList<EconomyRow> catalog)
    {
        if (confidence < 55 || text.Length > 160) return null;
        int quantity = 1;
        var stack = Regex.Match(text, @"^\s*(\d+)\s*[x×]\s+", RegexOptions.IgnoreCase);
        if (stack.Success)
        {
            if (!int.TryParse(stack.Groups[1].Value,out quantity) || quantity is <1 or >1000000) return null;
            text = text[stack.Length..];
        }
        string key = Normal(text);
        if (key.Length < 7) return null;
        var exact = catalog.Where(x => Normal(x.Name) == key).ToArray();
        if (exact.Length == 1) return new(exact[0], false, quantity);
        if (exact.Length > 1 || confidence < 75) return null;
        var scores = catalog.Where(x => Tier(x.Name) == Tier(text)).Select(x => (Row: x, Score: Similarity(key, Normal(x.Name)))).OrderByDescending(x => x.Score).Take(2).ToArray();
        if (scores.Length == 0 || scores[0].Score < .92 || (scores.Length > 1 && scores[0].Score - scores[1].Score < .04)) return null;
        return new(scores[0].Row, true, quantity);
    }
    private static double Similarity(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (int i = 1; i <= a.Length; i++)
        {
            var next = new int[b.Length + 1]; next[0] = i;
            for (int j = 1; j <= b.Length; j++) next[j] = Math.Min(Math.Min(next[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            previous = next;
        }
        return 1.0 - (double)previous[b.Length] / Math.Max(a.Length, b.Length);
    }
}
