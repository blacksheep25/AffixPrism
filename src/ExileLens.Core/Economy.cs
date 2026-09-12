using System.Text.Json;

namespace ExileLens.Core;

public sealed record EconomyRow(string Name, string BaseType, string Variant, decimal Value, string Currency, int? ListingCount, bool? Corrupted)
{
    public string VariantLabel => string.IsNullOrWhiteSpace(Variant) ? (Corrupted == true ? "Corrupted" : "Standard") : Variant + (Corrupted == true ? " · Corrupted" : "");
    public string PriceLabel => $"{Value:0.##} {Currency}";
    public string DetailLabel => string.Join(" · ", new[] { string.IsNullOrWhiteSpace(Variant) ? null : Variant, Corrupted == true ? "Corrupted" : null, ListingCount is { } n ? $"{n:N0} listings" : null }.Where(x => x != null));
}
public sealed record EconomySnapshot(IReadOnlyList<EconomyRow> Rows, DateTimeOffset FetchedAt, bool Cached, bool Stale, string Source = "poe.ninja");
public sealed record EconomyCategory(string Type, bool Exchange);

public static class Economy
{
    public static bool UsesExchange(CopiedItem item) => Category(item)?.Exchange == true;
    public static bool UsesEquipmentListings(CopiedItem item) => !ItemPresentation.IsQuest(item) && !UsesExchange(item) && !ItemPresentation.IsGem(item) && item.Rarity is "Normal" or "Magic" or "Rare" or "Unique";
    public static EconomyCategory? Category(CopiedItem item)
    {
        if (ItemPresentation.IsQuest(item)) return null;
        string c = item.ItemClass.ToLowerInvariant();
        if (c.Contains("uncut") || item.Name.StartsWith("Uncut ", StringComparison.OrdinalIgnoreCase)) return new("UncutGems", true);
        if (ItemPresentation.IsGem(item) && System.Text.RegularExpressions.Regex.IsMatch(item.Details, @"(?im)^(?:Support,\s*)?Lineage(?:,|$)")) return new("LineageSupportGems",true);
        if (ItemPresentation.IsGem(item)) return null;
        if (item.Rarity == "Unique")
        {
            if (c.Contains("jewel")) return new("UniqueJewels", false);
            if (c.Contains("charm")) return new("UniqueCharms", false);
            if (c.Contains("flask")) return new("UniqueFlasks", false);
            if (c.Contains("relic")) return new("UniqueSanctumRelics", false);
            if (c.Contains("tablet")) return new("UniqueTablets", false);
            if (c.Contains("ring") || c.Contains("amulet") || c.Contains("belt")) return new("UniqueAccessories", false);
            if (new[] { "boot", "glove", "helmet", "body armour", "shield", "focus", "quiver", "buckler" }.Any(c.Contains)) return new("UniqueArmours", false);
            if (new[] { "sword", "axe", "mace", "staff", "staves", "bow", "wand", "sceptre", "spear", "dagger", "claw", "flail" }.Any(c.Contains)) return new("UniqueWeapons", false);
            return null;
        }
        if (item.Rarity is "Rare" or "Magic") return null;
        string name = item.Name;
        if (c.Contains("omen") || name.StartsWith("Omen of ", StringComparison.Ordinal)) return new("Ritual", true);
        if (c.Contains("essence") || name.Contains("Essence of ", StringComparison.Ordinal)) return new("Essences", true);
        if (c.Contains("soul core") || name.StartsWith("Soul Core of ", StringComparison.Ordinal)) return new("SoulCores", true);
        if (c.Contains("rune")) return new("Runes", true);
        if (c.Contains("catalyst")) return new("Breach", true);
        if (c.Contains("fragment")) return new("Fragments", true);
        if (c.Contains("liquid emotion") || name.StartsWith("Distilled ", StringComparison.Ordinal)) return new("Delirium", true);
        if (c.Contains("expedition") || name is "Exotic Coinage" or "Burial Medallion" or "Astragali" or "Scrap Metal") return new("Expedition", true);
        return item.Rarity == "Currency" ? new("Currency", true) : null;
    }

    public static IReadOnlyList<EconomyRow> Matching(IReadOnlyList<EconomyRow> rows, CopiedItem item)
    {
        bool corrupted = ItemAnalysis.From(item).Corrupted;
        return rows.Where(x => MatchesIdentity(x, item)
            && (item.Rarity != "Unique" || string.IsNullOrWhiteSpace(x.BaseType) || string.Equals(x.BaseType, item.BaseType, StringComparison.OrdinalIgnoreCase))
            && (x.Corrupted == null || x.Corrupted == corrupted)).OrderBy(x => x.Value).ToArray();
    }

    private static bool MatchesIdentity(EconomyRow row, CopiedItem item)
    {
        if (Category(item)?.Type != "UncutGems")
            return string.Equals(row.Name, item.Name, StringComparison.OrdinalIgnoreCase);
        var level = System.Text.RegularExpressions.Regex.Match(item.Details, @"(?im)^Level: ?(\d+)");
        if (!level.Success) return false;
        string n = level.Groups[1].Value;
        return (string.Equals(row.Name, item.Name, StringComparison.OrdinalIgnoreCase)
                && (row.Variant == n || row.Variant.Equals("Level " + n, StringComparison.OrdinalIgnoreCase)))
            || string.Equals(row.Name, item.Name + " (Level " + n + ")", StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.Name, item.Name + " (" + n + ")", StringComparison.OrdinalIgnoreCase);
    }

    public static string? PrimaryCurrency(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("core", out var core) || !core.TryGetProperty("primary", out var primary)) return null;
        string? id = Scalar(primary);
        if (id == null) return null;
        return Metadata(core).TryGetValue(id, out var name) ? name : id;
    }
    public static IReadOnlyList<EconomyRow> Parse(string json, bool exchange, string? fallbackCurrency = null)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array) throw new JsonException("Missing economy lines");
        string? currency = PrimaryCurrency(json) ?? fallbackCurrency;
        if (string.IsNullOrWhiteSpace(currency)) throw new JsonException("Price currency was not supplied by the provider");
        var names = root.TryGetProperty("core", out var core) ? Metadata(core) : new Dictionary<string, string>();
        // Exchange item identities live at the root; core.items only describes pricing currencies.
        foreach(var entry in Metadata(root)) names[entry.Key]=entry.Value;
        var rows = new List<EconomyRow>();
        foreach (var line in lines.EnumerateArray())
        {
            if (line.ValueKind != JsonValueKind.Object) continue;
            if (!line.TryGetProperty("primaryValue", out var price) || price.ValueKind != JsonValueKind.Number || !price.TryGetDecimal(out decimal value) || value < 0) continue;
            string? name = Text(line, "name");
            if (exchange && name == null && line.TryGetProperty("id", out var id) && Scalar(id) is { } key) names.TryGetValue(key, out name);
            if (string.IsNullOrWhiteSpace(name)) continue;
            int? count = line.TryGetProperty("listingCount", out var n) && n.ValueKind == JsonValueKind.Number && n.TryGetInt32(out int countValue) ? countValue : null;
            bool? corrupted = line.TryGetProperty("corrupted", out var c) && c.ValueKind is JsonValueKind.True or JsonValueKind.False ? c.GetBoolean() : null;
            rows.Add(new(name, Text(line, "baseType") ?? "", Text(line, "variant") ?? "", value, currency, count, corrupted));
        }
        return rows;
    }
    private static string? Text(JsonElement element, string key) => element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string? Scalar(JsonElement element) => element.ValueKind == JsonValueKind.String ? element.GetString() : element.ValueKind == JsonValueKind.Number ? element.GetRawText() : null;
    private static Dictionary<string, string> Metadata(JsonElement core)
    {
        var names = new Dictionary<string, string>();
        if (core.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            foreach (var item in items.EnumerateArray())
                if (item.TryGetProperty("id", out var id) && Scalar(id) is { } key && Text(item, "name") is { } name) names[key] = name;
        return names;
    }
}
