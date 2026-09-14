namespace AffixPrism.Core;

// Category-based starting filters, not a market-value or build-quality ranking.
public static class DefaultItemFilters
{
    // Broad first search, as in EE2's ordinary modifier defaults. Profiles remain explicit opt-in.
    public static IReadOnlyList<string> Select(CopiedItem item) => Array.Empty<string>();
    public static IReadOnlyList<string> Suggested(CopiedItem item)
    {
        if (ItemAnalysis.From(item).Unidentified) return Array.Empty<string>();
        if (item.Rarity is not ("Rare" or "Magic") || !Economy.UsesEquipmentListings(item)) return Array.Empty<string>();
        var lines = ItemAnalysis.From(item).Lines;
        string category = item.ItemClass.ToLowerInvariant();
        bool caster = category is "wands" or "staves" or "sceptres";
        bool attack = !caster && lines.Any(l => l.Text.StartsWith("Total DPS:"));
        bool boots = category.Contains("boots");
        bool armour = new[] { "armour", "gloves", "helmets", "boots", "shields", "foci", "bucklers" }.Any(category.Contains);
        (int Score, string Group) Rank(ItemLine line)
        {
            string text = line.Text.ToLowerInvariant();
            if (line.Kind == "Property")
            {
                if (attack && text.StartsWith("total dps:")) return (120,"damage");
                if (attack && text.StartsWith("attacks per second:")) return (90,"speed");
                if (armour && text.StartsWith("energy shield:")) return (85,"defence");
                return (0,"");
            }
            if (line.Kind == "Pseudo") return text.Contains("total elemental resistance") && !attack && !caster ? (75,"resistance") : (0,"");
            if (line.Kind is "Rune" or "Implicit" or "Enchant" or "Flavour" or "Description" or "Instructions" or "State") return (0,"");
            if (text.Contains("to level of") && text.Contains("skills")) return (110,"skills");
            if (boots && text.Contains("increased movement speed")) return (120,"movement");
            if (!attack && !caster && text.Contains("to maximum life")) return (100,"life");
            if (caster && text.Contains("increased spell damage")) return (100,"damage");
            if (caster && text.Contains("increased cast speed")) return (90,"speed");
            if (caster && text.Contains("minions") && text.Contains("increased damage")) return (100,"damage");
            if (!attack && text.Contains("increased attack speed") && !text.Contains("minions") && !text.Contains("companions")) return (80,"speed");
            if (text.Contains("to maximum mana")) return (50,"mana");
            if (text.Contains("to all attributes")) return (40,"attributes");
            return (0,"");
        }
        return lines.Select(l => (Line:l, Rank:Rank(l))).Where(x => x.Rank.Score>0)
            .OrderByDescending(x => x.Rank.Score).GroupBy(x => x.Rank.Group)
            .Select(g=>g.First()).Take(3).Select(x=>x.Line.Text).ToArray();
    }
}
