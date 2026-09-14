namespace AffixPrism.Core;

// Clipboard and trade data share this display order; missing data is never copied from a peer.
public static class ItemPresentation
{
    public static bool IsQuest(CopiedItem item) => item.Rarity.Equals("Quest",StringComparison.OrdinalIgnoreCase) || item.ItemClass.Contains("Quest",StringComparison.OrdinalIgnoreCase);
    public static string WikiUrl(CopiedItem item) => "https://www.poe2wiki.net/wiki/"+Uri.EscapeDataString(item.Name.Replace(' ','_'));
    public static bool IsGem(CopiedItem item) => item.Rarity.Equals("Gem",StringComparison.OrdinalIgnoreCase) || item.ItemClass.Contains("Gems",StringComparison.OrdinalIgnoreCase);
    public static string ClassLabel(string text) => text.Trim() switch
    {
        "Body Armours" => "Body Armour", "Charms" => "Charm", "Quarterstaves" => "Quarterstaff", "Wands" => "Wand", "Quivers" => "Quiver", "Jewels" => "Jewel", "Unknown class" => "", _ => text.Trim()
    };
    public static bool IsClassLabel(string text) => new[] { "Jewel", "Jewels", "Body Armour", "Body Armours", "Gloves", "Boots", "Helmet", "Helmets", "Ring", "Rings", "Amulet", "Amulets", "Belt", "Belts", "Shield", "Shields", "Quiver", "Quivers", "Focus", "Foci", "Quarterstaff", "Quarterstaves", "Wand", "Wands", "Bow", "Bows", "Sceptre", "Sceptres" }.Contains(text);
    public static IReadOnlyList<ItemLine> Lines(CopiedItem item)
    {
        var lines = ItemAnalysis.From(item).Lines.ToList();
        string itemClass = ClassLabel(item.ItemClass);
        string? fromProperty = lines.FirstOrDefault(l => IsClassLabel(l.Text))?.Text;
        if (itemClass.Length == 0) itemClass = ClassLabel(fromProperty ?? "");
        lines.RemoveAll(l => IsClassLabel(l.Text));
        if (itemClass.Length > 0) lines.Insert(0,new(itemClass,"Class"));
        return lines.OrderBy(l => l.Kind switch { "Class" => 0, "Property" => 1, "Rune" => 3, "Enchant" => 4, "Implicit" => 5, "State" => 8, "Flavour" => 9, "Description" or "Instructions" => 10, _ => 6 })
            .ThenBy(l => l.Kind == "Property" ? PropertyOrder(l.Text) : 0).ToArray();
    }
    private static int PropertyOrder(string text)
    {
        string[] order = { "Quality:", "Physical Damage:", "Elemental Damage:", "Critical Hit Chance:", "Attacks per Second:", "Armour:", "Evasion Rating:", "Energy Shield:", "Requires:", "Sockets:", "Rune Sockets:", "Item Level:", "Grants Skill:" };
        int index = Array.FindIndex(order,p => text.StartsWith(p,StringComparison.Ordinal));
        return index < 0 ? order.Length : index;
    }
}
