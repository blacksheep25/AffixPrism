using System.Globalization;
using System.Text.RegularExpressions;

namespace ExileLens.Core;

public sealed record OverviewStat(string Name, decimal? Yours, decimal? Other, bool Points = false)
{
    public int Direction => Yours.HasValue && Other.HasValue ? Math.Sign(Other.Value-Yours.Value) : 0;
    public string Difference => !Yours.HasValue || !Other.HasValue ? "Unavailable" : Direction==0 ? "Same" :
        Points ? (Other!.Value-Yours!.Value).ToString("+0.##;-0.##",CultureInfo.InvariantCulture)+(Name.Contains("Level of",StringComparison.OrdinalIgnoreCase) ? " levels" : " points") :
        Yours==0 ? (Other!.Value-Yours!.Value).ToString("+0.##;-0.##",CultureInfo.InvariantCulture) :
        ((Other!.Value-Yours!.Value)/Math.Abs(Yours!.Value)*100).ToString("+0.#;-0.#",CultureInfo.InvariantCulture)+"%";
}
public sealed record ComparisonOverview(string Verdict, IReadOnlyList<OverviewStat> Stats, string Gains, string Losses)
{
    public static readonly string[] Priorities = { "General", "Physical attacks", "Elemental attacks", "Spells", "Defences" };
    public static ComparisonOverview Create(CopiedItem yours, CopiedItem other, string priority)
    {
        var a=ItemAnalysis.From(yours); var b=ItemAnalysis.From(other);
        decimal? Property(ItemAnalysis item,string name)
        {
            var line=item.Lines.FirstOrDefault(l=>l.Kind=="Property" && l.Text.StartsWith(name+":",StringComparison.Ordinal));
            if(line==null) return null;
            var m=Regex.Match(line.Text[(name.Length+1)..],@"\d+(?:\.\d+)?");
            return m.Success ? decimal.Parse(m.Value,CultureInfo.InvariantCulture) : null;
        }
        var stats=new List<OverviewStat>();
        void Add(string name,bool points=false) { var x=Property(a,name); var y=Property(b,name); if(x.HasValue || y.HasValue) stats.Add(new(name,x,y,points)); }
        if(priority is "Physical attacks" or "Elemental attacks")
        {
            Add(priority=="Physical attacks" ? "Physical DPS" : "Elemental DPS"); Add("Total DPS"); Add("Attacks per Second"); Add("Critical Hit Chance",true);
        }
        if(priority=="Defences") { Add("Armour"); Add("Evasion Rating"); Add("Energy Shield"); }
        bool Relevant(ItemLine l) => l.Kind is "Explicit" or "Item text" or "Implicit" or "Rune" or "Enchant";
        if(priority=="General")
        {
            Add("Total DPS"); Add("Attacks per Second"); Add("Critical Hit Chance",true);
            Add("Armour"); Add("Evasion Rating"); Add("Energy Shield");
            foreach(var row in ItemComparison.Rows(yours,other).Where(r=>Relevant(new ItemLine("",r.Kind))))
            {
                decimal[] Values(string text)=>Regex.Matches(text,@"[+-]?\d+(?:\.\d+)?").Select(m=>decimal.Parse(m.Value,CultureInfo.InvariantCulture)).ToArray();
                var x=Values(row.Yours); var y=Values(row.Seller); var text=row.Yours=="—" ? row.Seller : row.Yours;
                var label=Regex.Replace(text,@"[+-]?\d+(?:\.\d+)?%?\s*","").Trim();
                if(row.Kind is "Implicit" or "Rune" or "Enchant")label+=" ("+row.Kind.ToLowerInvariant()+")";
                for(int i=0;i<Math.Max(x.Length,y.Length);i++)stats.Add(new(label+(Math.Max(x.Length,y.Length)>1 ? $" · value {i+1}" : ""),i<x.Length?x[i]:null,i<y.Length?y[i]:null,true));
            }
        }
        bool Wanted(string text) => text.Contains("to Level of",StringComparison.OrdinalIgnoreCase) ||
            (priority=="Spells" && (text.Contains("increased Spell Damage",StringComparison.OrdinalIgnoreCase) || text.Contains("increased Cast Speed",StringComparison.OrdinalIgnoreCase))) ||
            (priority=="Defences" && (text.Contains("to maximum Life",StringComparison.OrdinalIgnoreCase) || text.Contains("Resistance",StringComparison.OrdinalIgnoreCase)));
        foreach(var line in a.Lines.Concat(b.Lines).Where(l=>priority!="General"&&Relevant(l)&&Wanted(l.Text)).DistinctBy(l=>ComparableMarket.Signature(l.Text)))
        {
            decimal? Value(ItemAnalysis item) { var match=item.Lines.FirstOrDefault(l=>Relevant(l)&&ComparableMarket.Signature(l.Text)==ComparableMarket.Signature(line.Text)); if(match==null) return null; var number=Regex.Match(match.Text,@"[+-]?\d+(?:\.\d+)?"); return number.Success ? decimal.Parse(number.Value,CultureInfo.InvariantCulture) : null; }
            stats.Add(new(Regex.Replace(line.Text,@"[+-]?\d+(?:\.\d+)?%?\s*","",RegexOptions.None).Trim(),Value(a),Value(b),true));
        }
        string verdict;
        if(a.Unidentified || b.Unidentified || yours.ItemClass!=other.ItemClass) verdict="Insufficient comparable information";
        else if(stats.Count==0) verdict="No numeric stats to compare · see the item cards";
        else if(stats.Any(s=>!s.Yours.HasValue || !s.Other.HasValue)) verdict="Trade-off · some relevant stats are unavailable";
        else if(stats.All(s=>s.Direction==0)) verdict="Very similar on the displayed stats";
        else if(stats.Any(s=>s.Direction>0) && stats.Any(s=>s.Direction<0)) verdict="Trade-off · review the gains and losses";
        else verdict=(stats.Any(s=>s.Direction>0) ? "Other item" : "Your item")+(priority=="General" ? " has higher displayed values · assess their effects for your build" : " is stronger on the displayed "+priority.ToLowerInvariant()+" stats");
        return new(verdict,stats,string.Join(", ",stats.Where(s=>s.Direction>0).Select(s=>s.Name+" "+s.Difference)),string.Join(", ",stats.Where(s=>s.Direction<0).Select(s=>s.Name+" "+s.Difference)));
    }
}
