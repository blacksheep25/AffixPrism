using System.Text.Json;
using System.Text.RegularExpressions;
namespace ExileLens.Core;
public static class SocketAugments
{
    public sealed record MatchGroup(string Name,string Details);
    private static string Normalize(string text) => Regex.Replace(ItemAnalysis.CleanTradeText(text), @"\+(?=\d)", "").Trim();
    public static IReadOnlyList<MatchGroup>? MatchGroups(string itemClass,IReadOnlyList<string> effects,int socketCount)
    {
        if(socketCount<1||socketCount>12||effects.Count>30)return null;
        string category=Category(itemClass);
        var remaining=effects.Select(Normalize).ToList();
        var candidates=catalog.Select(a=>new { a.Name, Effects=a.Effects.Where(e=>e.Categories.Contains(category)).Select(e=>Normalize(e.Text)).ToArray() })
            .Where(a=>a.Effects.Length>0&&a.Effects.All(e=>remaining.Contains(e,StringComparer.OrdinalIgnoreCase))).ToArray();
        var solutions=new List<MatchGroup[]>(); int attempts=0;
        void Search(List<string> rest,List<MatchGroup> chosen,int start)
        {
            if(++attempts>10000||solutions.Count>1)return;
            if(chosen.Count==socketCount) { if(rest.Count==0)solutions.Add(chosen.ToArray()); return; }
            for(int i=start;i<candidates.Length && attempts<=10000 && solutions.Count<2;i++)
            {
                var candidate=candidates[i]; var next=new List<string>(rest); bool fits=true;
                foreach(var effect in candidate.Effects) { int index=next.FindIndex(e=>e.Equals(effect,StringComparison.OrdinalIgnoreCase)); if(index<0){fits=false;break;} next.RemoveAt(index); }
                if(!fits)continue;
                chosen.Add(new(candidate.Name,string.Join("\n",candidate.Effects))); Search(next,chosen,i); chosen.RemoveAt(chosen.Count-1);
            }
        }
        Search(remaining,new(),0);
        return attempts<=10000&&solutions.Count==1 ? solutions[0] : null;
    }
    private static string Category(string itemClass) => itemClass switch {
            "Quarterstaves"=>"Warstaff", "Body Armours"=>"Body Armour", "Helmets"=>"Helmet", "Bows"=>"Bow",
            "Crossbows"=>"Crossbow", "Wands"=>"Wand", "Staves"=>"Staff", "Sceptres"=>"Sceptre", "Shields"=>"Shield",
            "Foci"=>"Focus", "Bucklers"=>"Buckler", "Spears"=>"Spear", "Daggers"=>"Dagger", "Claws"=>"Claw",
            "One Hand Maces"=>"One Hand Mace", "Two Hand Maces"=>"Two Hand Mace", _=>itemClass };
    private sealed record Effect(string[] Categories,string Text);
    private sealed record Augment(string Name,string Icon,Effect[] Effects);
    private static readonly Augment[] catalog=Load();
    private static Augment[] Load()
    {
        using var stream=typeof(SocketAugments).Assembly.GetManifestResourceStream("ExileLens.Core.Data.socket-augments.json")!;
        return JsonSerializer.Deserialize<Augment[]>(stream) ?? Array.Empty<Augment>();
    }
    public static string? Icon(string name)=>ItemArtwork.SafeUrl(catalog.FirstOrDefault(a=>a.Name.Equals(name.Trim(),StringComparison.OrdinalIgnoreCase))?.Icon);
    public static string? Match(string itemClass,string effect)
    {
        // Clipboard text includes a leading '+' for additive values; catalogue
        // effects omit it. Keep actual values and negative signs significant.
        string category=Category(itemClass);
        var matches=catalog.Where(a=>a.Effects.Any(e=>e.Categories.Contains(category) && Normalize(e.Text).Equals(Normalize(effect),StringComparison.OrdinalIgnoreCase))).ToArray();
        return matches.Length==1 ? matches[0].Name : null;
    }
}
