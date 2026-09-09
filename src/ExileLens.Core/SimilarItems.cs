using System.Globalization;
using System.Text.RegularExpressions;
namespace ExileLens.Core;

public sealed record SimilarEstimate(decimal? Price, int Sellers, decimal Similarity, CopiedItem? Average, string Explanation);
public static class SimilarItems
{
    private static readonly Regex Numbers = new(@"(?<![\d.])[+-]?\d+(?:\.\d+)?");
    private static decimal[] Values(string text) => Numbers.Matches(text).Select(m => decimal.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
    private static ItemLine[] Mods(CopiedItem item) => ItemAnalysis.From(item).Lines.Where(l => l.Kind is not ("Pseudo" or "Property" or "State" or "Flavour" or "Description") && Numbers.IsMatch(l.Text)).ToArray();
    public static decimal Score(CopiedItem yours, CopiedItem other)
    {
        var a = ItemAnalysis.From(yours); var b = ItemAnalysis.From(other);
        if (yours.BaseType != other.BaseType || yours.Rarity != other.Rarity || a.CorruptionLevel != b.CorruptionLevel || a.Unidentified != b.Unidentified || yours.Rarity == "Unique" && !a.Unidentified && yours.Name != other.Name) return 0;
        foreach (var critical in a.Lines.Where(l => l.Text.Contains("to Level of",StringComparison.OrdinalIgnoreCase) || l.Text.Contains("additional Arrow",StringComparison.OrdinalIgnoreCase)))
            if (!b.Lines.Any(l=>l.Kind==critical.Kind && ComparableMarket.Signature(l.Text)==ComparableMarket.Signature(critical.Text) && Values(l.Text).SequenceEqual(Values(critical.Text)))) return 0;
        var myDps=a.Lines.FirstOrDefault(l=>l.Text.StartsWith("Total DPS:"));
        if(myDps != null)
        {
            var theirDps=b.Lines.FirstOrDefault(l=>l.Text.StartsWith("Total DPS:"));
            if(theirDps==null || Values(theirDps.Text)[0] < Values(myDps.Text)[0]*.85m || Values(theirDps.Text)[0] > Values(myDps.Text)[0]*1.2m) return 0;
        }
        var mine = Mods(yours); var theirs = Mods(other).ToList();
        if (mine.Length == 0) return 0;
        decimal sum = 0; int total = Math.Max(mine.Length, theirs.Count);
        foreach (var mod in mine)
        {
            var match = theirs.FirstOrDefault(l => l.Kind == mod.Kind && ComparableMarket.Signature(l.Text) == ComparableMarket.Signature(mod.Text));
            if (match == null) continue;
            theirs.Remove(match);
            var x = Values(mod.Text); var y = Values(match.Text);
            sum += x.Zip(y, (v,w) => Math.Sign(v) != Math.Sign(w) ? 0m : 1 - Math.Min(1, Math.Abs(v-w)/Math.Max(1,Math.Max(Math.Abs(v),Math.Abs(w))))).Average();
        }
        return total == 0 ? 0 : sum / total;
    }
    public static decimal? ConvertedPrice(ComparableListing row, ComparableResult result)
    {
        if (row.Listing.Price.Currency.Equals(result.Currency,StringComparison.OrdinalIgnoreCase)) return row.Listing.Price.Amount;
        if (result.ExchangeRates != null && result.ExchangeRates.TryGetValue(row.Listing.Price.Currency,out var rate) && rate>0)
            return row.Listing.Price.Amount * rate;
        return null;
    }
    public static SimilarEstimate Estimate(CopiedItem item, ComparableResult result)
    {
        if (ItemAnalysis.From(item).Unidentified) return new(null,result.SellerCount,0,null,"Unidentified base-item listings only. Hidden identity and modifiers cannot be valued reliably.");
        var peers = result.Rows.Select(r => (Row:r, Score:Score(item,r.Item), Price:ConvertedPrice(r,result))).Where(r=>r.Price.HasValue).Where(r => r.Score >= .8m)
            .GroupBy(r => r.Row.Account.Trim(), StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(r => r.Score).ThenBy(r => r.Price).First()).OrderByDescending(p=>p.Score).Take(10).ToArray();
        if (peers.Length < 3) return new(null,peers.Length,0,null,$"No recommendation: {peers.Length} qualifying independent sellers; at least 3 with 80% modifier/roll similarity are required. " + PriceDiagnostics.From(item,result).Summary);
        var prices = peers.Select(r => r.Price!.Value).Order().ToArray();
        decimal price = (prices[(prices.Length-1)/2]+prices[prices.Length/2])/2;
        var lines = ItemAnalysis.From(item).Lines.Select(line =>
        {
            var matches = peers.Select(p => ItemAnalysis.From(p.Row.Item).Lines.FirstOrDefault(l => l.Kind == line.Kind && ComparableMarket.Signature(l.Text) == ComparableMarket.Signature(line.Text)))
                .Where(l => l != null).Select(l => Values(l!.Text)).ToArray();
            int count = Values(line.Text).Length;
            if (count == 0 || matches.Length == 0) return line.Text;
            int index = 0;
            string averaged = Numbers.Replace(line.Text, m => { int i=index++; var nums=matches.Where(v => v.Length>i).Select(v => v[i]).ToArray(); return nums.Length == 0 ? m.Value : nums.Average().ToString("0.##",CultureInfo.InvariantCulture); });
            return averaged + (line.Kind switch { "Implicit" => " (implicit)", "Rune" => " (rune)", "Enchant" => " (enchant)", _ => "" });
        });
        var average = new CopiedItem("Average of similar listings", item.BaseType,item.Rarity,item.ItemClass,
            $"Item Class: {item.ItemClass}\nRarity: {item.Rarity}\nAverage of similar listings\n{item.BaseType}\n--------\n"+string.Join("\n",lines));
        return new(price,peers.Length,peers.Average(p=>p.Score),average,"Suggested asking price · similar-listing median, not completed sales. Limited to the fetched sample.");
    }
}
