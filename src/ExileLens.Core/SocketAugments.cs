using System.Text.Json;
namespace ExileLens.Core;
public static class SocketAugments
{
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
        string category=itemClass switch {
            "Quarterstaves"=>"Warstaff", "Body Armours"=>"Body Armour", "Helmets"=>"Helmet", "Bows"=>"Bow",
            "Crossbows"=>"Crossbow", "Wands"=>"Wand", "Staves"=>"Staff", "Sceptres"=>"Sceptre", "Shields"=>"Shield",
            "Foci"=>"Focus", "Bucklers"=>"Buckler", "Spears"=>"Spear", "Daggers"=>"Dagger", "Claws"=>"Claw",
            "One Hand Maces"=>"One Hand Mace", "Two Hand Maces"=>"Two Hand Mace", _=>itemClass };
        var matches=catalog.Where(a=>a.Effects.Any(e=>e.Categories.Contains(category) && e.Text.Equals(effect,StringComparison.OrdinalIgnoreCase))).ToArray();
        return matches.Length==1 ? matches[0].Name : null;
    }
}
