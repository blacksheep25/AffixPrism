namespace AffixPrism.Core;

public sealed record CopiedItem(string Name, string BaseType, string Rarity, string ItemClass, string Details, IReadOnlyList<ItemSocket>? Sockets = null);
public sealed record ItemSocket(int Index, string Kind, string? Name, string? IconUrl, bool? Occupied, string? Details = null, bool Inferred = false);

public static class ItemParser
{
    public static CopiedItem? Parse(string text)
    {
        if (text.Length > 100_000) return null;
        var lines = text.TrimStart('\uFEFF').Replace("\r", "").Split('\n').Select(x => x.TrimEnd()).ToArray();
        int rarityIndex = Array.FindIndex(lines, l => l.StartsWith("Rarity: ", StringComparison.Ordinal));
        if (rarityIndex < 0) return null;
        var names = lines.Skip(rarityIndex + 1).TakeWhile(l => l != "--------").Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        if (names.Length is < 1 or > 2) return null;
        string rarity = lines[rarityIndex][8..].Trim();
        if (rarity.Length == 0) return null;
        string itemClass = lines.FirstOrDefault(l => l.StartsWith("Item Class: ", StringComparison.Ordinal))?[12..] ?? "Unknown class";
        string baseType = names[^1];
        if (rarity == "Magic" && names.Length == 1)
            baseType = BaseTypes.ResolveMagicName(names[0]) ?? baseType;
        return new(names[0], baseType, rarity, itemClass, text.Trim());
    }
}
