using System.Globalization;
using System.Text.RegularExpressions;
namespace AffixPrism.Core;
public static class PseudoStats
{
    public static IReadOnlyList<ItemLine> Calculate(IReadOnlyList<ItemLine> lines)
    {
        var totals = new Dictionary<string,decimal>();
        void Add(string key,decimal value) => totals[key]=totals.GetValueOrDefault(key)+value;
        foreach(var line in lines.Where(l=>l.Kind is not ("Property" or "Pseudo" or "State" or "Flavour" or "Description" or "Instructions")))
        {
            var m=Regex.Match(line.Text,@"^([+-]?\d+)% to (all Elemental Resistances|(?:Fire|Cold|Lightning|Chaos)(?: and (?:Fire|Cold|Lightning|Chaos))? Resistances?)$",RegexOptions.IgnoreCase);
            if(m.Success)
            {
                decimal value=decimal.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture);
                string text=m.Groups[2].Value;
                foreach(var element in new[] {"Fire","Cold","Lightning","Chaos"})
                    if(text.Contains(element,StringComparison.OrdinalIgnoreCase) || element!="Chaos" && text.StartsWith("all",StringComparison.OrdinalIgnoreCase)) Add(element,value);
            }
            m=Regex.Match(line.Text,@"^([+-]?\d+) to (all Attributes|(?:Strength|Dexterity|Intelligence)(?: and (?:Strength|Dexterity|Intelligence))?)$",RegexOptions.IgnoreCase);
            if(m.Success)
            {
                decimal value=decimal.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture);
                string text=m.Groups[2].Value;
                foreach(var attr in new[] {"Strength","Dexterity","Intelligence"})
                    if(text.Contains(attr,StringComparison.OrdinalIgnoreCase) || text.Equals("all Attributes",StringComparison.OrdinalIgnoreCase)) Add(attr,value);
                if(text.Equals("all Attributes",StringComparison.OrdinalIgnoreCase)) Add("all Attributes",value);
            }
            m=Regex.Match(line.Text,@"^([+-]?\d+) to maximum (Life|Mana|Energy Shield)$",RegexOptions.IgnoreCase);
            if(m.Success) Add(m.Groups[2].Value,decimal.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture));
        }
        var result=new List<ItemLine>();
        void Emit(string text) => result.Add(new(text,"Pseudo","Calculated aggregate from unconditional modifiers; conditional stats excluded."));
        string N(decimal value)=>value.ToString("0.##",CultureInfo.InvariantCulture);
        foreach(var element in new[] {"Fire","Cold","Lightning","Chaos"})
            if(totals.TryGetValue(element,out var value)) Emit(N(value)+"% total to "+element+" Resistance");
        if(new[] {"Fire","Cold","Lightning"}.Any(totals.ContainsKey)) Emit(N(totals.GetValueOrDefault("Fire")+totals.GetValueOrDefault("Cold")+totals.GetValueOrDefault("Lightning"))+"% total Elemental Resistance");
        foreach(var attr in new[] {"Strength","Dexterity","Intelligence","all Attributes"})
            if(totals.TryGetValue(attr,out var value)) Emit("+"+N(value)+" total to "+attr);
        if(totals.TryGetValue("Life",out var life)) Emit("+"+N(life+2*totals.GetValueOrDefault("Strength"))+" total maximum Life");
        if(totals.TryGetValue("Mana",out var mana)) Emit("+"+N(mana+2*totals.GetValueOrDefault("Intelligence"))+" total maximum Mana");
        if(totals.TryGetValue("Energy Shield",out var es)) Emit("+"+N(es)+" total maximum Energy Shield");
        return result;
    }
}
