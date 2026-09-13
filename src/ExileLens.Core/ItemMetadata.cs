namespace ExileLens.Core;

public sealed record MetadataStyle(string Name, string Colour, string UseCase);
public static class ItemMetadata
{
    public static readonly IReadOnlyList<MetadataStyle> Styles = new[] {
        new MetadataStyle("Gem description", "#D6C58C", "Support behaviour description, italic."),
        new MetadataStyle("Gem tags", "#B4B1A5", "Gem tags such as Lineage and Chaos."),
        new MetadataStyle("Instructions", "#96968E", "Socketing and use instructions, italic."),
        new MetadataStyle("Explicit", "#A3A4F2", "Ordinary prefix and suffix stats; tiers and roll ranges remain in tooltips."),
        new MetadataStyle("Implicit", "#B4B1A5", "Ordinary base implicit stats use neutral grey, separated from violet explicit stats."),
        new MetadataStyle("Enchant", "#B4E6FF", "Enchantments, identified by source metadata."),
        new MetadataStyle("Rune", "#B4E6FF", "Rune and soul-core socket effects."),
        new MetadataStyle("Crafted", "#B4E6FF", "Crafted modifier; never implies desecration."),
        new MetadataStyle("Desecrated", "#B2D153", "Desecrated modifier and desecrated header accents."),
        new MetadataStyle("Fractured", "#D7B578", "Locked fractured modifier."),
        new MetadataStyle("Mutated", "#D784C8", "Mutated modifier; distinct overlay colour."),
        new MetadataStyle("Vestigial", "#A6A198", "Legacy vestigial modifier; retained when present."),
        new MetadataStyle("Bonded", "#B4E6FF", "Conditional bonded effects, with their condition retained."),
        new MetadataStyle("Scourge", "#E67878", "Legacy scourge modifiers."),
        new MetadataStyle("Crucible", "#D7B578", "Legacy allocated crucible effects."),
        new MetadataStyle("Utility", "#A3A4F2", "Utility effects supplied by the item source."),
        new MetadataStyle("Cosmetic", "#A6A198", "Cosmetic effects, not a power or price score."),
        new MetadataStyle("Property", "#B4B1A5", "Base properties, requirements and granted skills."),
        new MetadataStyle("Class", "#B4B1A5", "Item class heading."),
        new MetadataStyle("Flavour", "#AF6025", "Lore and quotations, italic."),
        new MetadataStyle("Description", "#A38D6D", "Usage instructions and item descriptions."),
        new MetadataStyle("Corrupted", "#FF3030", "Corrupted state."),
        new MetadataStyle("Twice Corrupted", "#D65BE8", "Double corruption, with skull icon."),
        new MetadataStyle("Unidentified", "#FF3030", "Unidentified state; hidden stats are not invented."),
        new MetadataStyle("Sanctified", "#E5D49A", "Sanctified state."),
        new MetadataStyle("Mirrored", "#A6D8DC", "Duplicated/mirrored state."),
        new MetadataStyle("Split", "#B4B1A5", "Split state."),
        new MetadataStyle("Unmodifiable", "#FF7979", "Restrictions on modification."),
        new MetadataStyle("Veiled", "#B2D153", "Unrevealed state; concealed modifier values remain unknown."),
        new MetadataStyle("Unknown", "#B4B1A5", "Unrecognised metadata: retain text and raw metadata without guessing its meaning.")
    };
    public static string Type(ItemLine line)
    {
        if (line.Kind == "State") return line.Text switch { "Fractured Item" => "Fractured", "Desecrated" => "Desecrated", "Unmodifiable except Chaos" => "Unmodifiable", _ => Styles.Any(s => s.Name == line.Text) ? line.Text : "Unknown" };
        string metadata = (line.Metadata ?? "").Replace(" · Unscalable Value", "");
        foreach (string type in new[] { "Fractured", "Crafted", "Desecrated", "Mutated", "Vestigial", "Bonded", "Scourge", "Crucible", "Utility", "Cosmetic" })
            if (metadata.Contains(type, StringComparison.OrdinalIgnoreCase)) return type;
        if (line.Text.StartsWith("Bonded:",StringComparison.Ordinal)) return "Bonded";
        if (Styles.Any(s => s.Name == line.Kind)) return line.Kind;
        if (metadata.Contains("Unique Modifier",StringComparison.OrdinalIgnoreCase)) return "Explicit";
        if (metadata.Length > 0 && !metadata.Contains("Prefix",StringComparison.OrdinalIgnoreCase) && !metadata.Contains("Suffix",StringComparison.OrdinalIgnoreCase) && !metadata.Contains("Explicit",StringComparison.OrdinalIgnoreCase)) return "Unknown";
        return "Explicit";
    }
    public static MetadataStyle Style(ItemLine line) => Styles.First(s => s.Name == Type(line));
}
