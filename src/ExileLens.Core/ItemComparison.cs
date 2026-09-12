using System.Globalization;
using System.Text.RegularExpressions;
namespace ExileLens.Core;
public sealed record ComparisonRow(string Yours, string Seller, string Change, string Kind, bool Changed, string? YoursMetadata = null, string? SellerMetadata = null);
public static class ItemComparison
{
    public static IReadOnlyList<ComparisonRow> Rows(CopiedItem baseline, CopiedItem current)
    {
        bool Comparable(ItemLine x) => x.Kind is not ("Pseudo" or "Description" or "Instructions" or "Flavour" or "Gem description" or "Gem tags" or "Class") && (Regex.IsMatch(x.Text, @"\d") || x.Kind == "State");
        var before = ItemAnalysis.From(baseline).Lines.Where(Comparable).ToList();
        var after = ItemAnalysis.From(current).Lines.Where(Comparable).ToList();
        var rows = new List<ComparisonRow>();
        foreach (var old in before)
        {
            int match = after.FindIndex(x => x.Kind == old.Kind && x.Text == old.Text);
            if (match < 0) match = after.FindIndex(x => x.Kind == old.Kind && ComparableMarket.Signature(x.Text) == ComparableMarket.Signature(old.Text));
            if (match < 0) { rows.Add(new(old.Text, "—", "Yours only", old.Kind, true, old.Metadata)); continue; }
            var next = after[match]; after.RemoveAt(match);
            decimal[] Values(string text) => Regex.Matches(text, @"(?<![\d.])[+-]?\d+(?:\.\d+)?").Select(x => decimal.Parse(x.Value, CultureInfo.InvariantCulture)).ToArray();
            bool changed = old.Text != next.Text;
            string delta = changed ? string.Join(" / ", Values(next.Text).Zip(Values(old.Text)).Select(x => (x.First - x.Second).ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture))) : "Same";
            rows.Add(new(old.Text, next.Text, delta, old.Kind, changed || old.Metadata != next.Metadata, old.Metadata, next.Metadata));
        }
        rows.AddRange(after.Select(x => new ComparisonRow("—", x.Text, "Seller only", x.Kind, true, null, x.Metadata)));
        return rows;
    }
    public static string Describe(CopiedItem baseline, CopiedItem current)
    {
        var previous = ItemAnalysis.From(baseline).Lines;
        var next = ItemAnalysis.From(current).Lines;
        var output = new List<string> { baseline.Name + " → " + current.Name, "Positive/negative means numerical change, not necessarily an upgrade.", "" };
        var consumed = new HashSet<int>();
        foreach (var line in next)
        {
            int index = -1;
            for (int i = 0; i < previous.Count; i++) if (!consumed.Contains(i) && ComparableMarket.Signature(previous[i].Text) == ComparableMarket.Signature(line.Text) && previous[i].Kind == line.Kind) { index = i; break; }
            if (index < 0) { output.Add("Added: " + line.Text); continue; }
            consumed.Add(index);
            if (line.Text == previous[index].Text) continue;
            var oldNumbers = Regex.Matches(previous[index].Text, @"(?<![\d.])[+-]?\d+(?:\.\d+)?").Select(x => decimal.Parse(x.Value, CultureInfo.InvariantCulture)).ToArray();
            var newNumbers = Regex.Matches(line.Text, @"(?<![\d.])[+-]?\d+(?:\.\d+)?").Select(x => decimal.Parse(x.Value, CultureInfo.InvariantCulture)).ToArray();
            string delta = string.Join(" / ", newNumbers.Zip(oldNumbers).Select(x => (x.First - x.Second).ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture)));
            output.Add(line.Text + "  [" + delta + "]");
        }
        for (int i = 0; i < previous.Count; i++) if (!consumed.Contains(i)) output.Add("Removed: " + previous[i].Text);
        if (output.Count == 3) output.Add("No property or modifier changes.");
        return string.Join(Environment.NewLine, output);
    }
}
