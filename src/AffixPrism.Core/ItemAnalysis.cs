using System.Globalization;
using System.Text.RegularExpressions;

namespace AffixPrism.Core;

public sealed record ItemRoll(decimal Value, decimal Minimum, decimal Maximum)
{
    // Position within the copied range; this is not a value or desirability score.
    public decimal? Position => Maximum > Minimum && Value >= Minimum && Value <= Maximum ? (Value - Minimum) * 100 / (Maximum - Minimum) : null;
}
public sealed record ItemLine(string Text, string Kind, string? Metadata = null, int? Tier = null, IReadOnlyList<ItemRoll>? Rolls = null)
{
    public bool IsCrafted => Metadata?.Contains("Crafted", StringComparison.OrdinalIgnoreCase) == true;
    public bool IsDesecrated => Metadata?.Contains("Desecrated", StringComparison.OrdinalIgnoreCase) == true;
    public string TierLabel => Tier is { } tier ? $"T{tier}" : "";
    public string RollLabel => Rolls?.Count == 1 && Rolls[0].Position is { } position ? $"{position:0}%" : "";
    public string Tooltip => string.Join("\n", new[] { Metadata, Rolls is { Count: > 0 } ? string.Join("; ", Rolls.Select(x => $"Roll {x.Value}: {x.Minimum} to {x.Maximum}")) : null }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
public sealed record ItemAnalysis(int? ItemLevel, int? Quality, bool Corrupted, bool Unidentified, IReadOnlyList<ItemLine> Lines)
{
    public bool Desecrated => Lines.Any(x => x.IsDesecrated || x.Text == "Desecrated");
    public bool TwiceCorrupted => Lines.Any(x => x.Text == "Twice Corrupted");
    public int CorruptionLevel => TwiceCorrupted ? 2 : Corrupted ? 1 : 0;
    public static string CleanTradeText(string text) => Regex.Replace(text, @"\[([^\[\]\r\n]+)\]", match =>
    {
        string token = match.Groups[1].Value;
        int separator = token.IndexOf('|');
        return separator >= 0 ? token[(separator + 1)..] : token;
    });
    private const string NumberPattern = @"[+-]?\d+(?:\.\d+)?";
    private static readonly Regex Range = new($@"(?<value>{NumberPattern})\((?<min>{NumberPattern})-(?<max>{NumberPattern})\)", RegexOptions.CultureInvariant);
    private static readonly Regex FixedRoll = new($@"(?<value>{NumberPattern})\((?<reference>{NumberPattern})\)", RegexOptions.CultureInvariant);
    private static readonly Regex TierPattern = new(@"\(Tier:\s*(\d+)\)", RegexOptions.CultureInvariant);
    public static ItemAnalysis From(CopiedItem item)
    {
        string[] lines = item.Details.TrimStart('\uFEFF').Replace("\r", "").Split('\n').Select(x => x.Trim()).ToArray();
        static int? Number(string[] source, string label)
        {
            string? line = source.FirstOrDefault(x => x.StartsWith(label + ":", StringComparison.Ordinal));
            var match = Regex.Match(line == null ? "" : line[(label.Length + 1)..], @"\d+");
            return match.Success && int.TryParse(match.Value, CultureInfo.InvariantCulture, out int n) ? n : null;
        }
        var content = new List<ItemLine>();
        string? metadata = null;
        bool inMetadata = false;
        bool quest = ItemPresentation.IsQuest(item); bool questInstructions=false;
        bool gem = ItemPresentation.IsGem(item);
        bool essence = item.Rarity == "Currency" && item.Name.Contains("Essence", StringComparison.OrdinalIgnoreCase);
        bool jewelInstructions = false;
        bool usageInstructions = false;
        string? gemSection = null; bool gemEffectSeen = false;
        int headerEnd = Array.IndexOf(lines, "--------");
        foreach (string source in lines.Skip(Math.Max(0, headerEnd + 1)))
        {
            string raw = CleanTradeText(source);
            if (raw == "--------") { gemSection = null; jewelInstructions = false; usageInstructions = false; metadata = null; inMetadata = false; continue; }
            if (raw.Length == 0) continue;
            if (raw.StartsWith('{') || inMetadata)
            {
                metadata = inMetadata ? metadata + " " + raw : raw;
                inMetadata = !raw.EndsWith('}');
                continue;
            }
            string kind = raw.EndsWith("(implicit)", StringComparison.Ordinal) || metadata?.Contains("Implicit Modifier", StringComparison.Ordinal) == true ? "Implicit" :
                raw.EndsWith("(enchant)", StringComparison.Ordinal) || metadata?.Contains("Enchant", StringComparison.Ordinal) == true ? "Enchant" :
                raw.EndsWith("(rune)", StringComparison.Ordinal) || metadata?.Contains("Rune", StringComparison.Ordinal) == true ? "Rune" :
                raw.Contains(':') && metadata == null ? "Property" : raw is "Corrupted" or "Twice Corrupted" or "Unidentified" or "Desecrated" or "Fractured Item" or "Sanctified" or "Mirrored" or "Split" or "Unmodifiable" or "Unmodifiable except Chaos" or "Veiled" ? "State" : "Item text";
            if (kind == "Item text" && metadata == null && !Regex.IsMatch(raw, @"\d") && item.Rarity == "Unique") kind = "Flavour";
            if (item.Rarity == "Currency" && kind == "Item text") kind = "Description";
            if (metadata == "{ Flavour Text }") kind = "Flavour";
            if (metadata == "{ Description }") kind = "Description";
            if (item.ItemClass.Contains("Charm", StringComparison.OrdinalIgnoreCase) || item.ItemClass.Contains("Flask", StringComparison.OrdinalIgnoreCase))
            {
                if (raw.StartsWith("Lasts ") || raw.StartsWith("Consumes ") || raw.StartsWith("Currently has ")) kind = "Property";
                if (raw.StartsWith("Grants Immunity to ")) kind = "Implicit";
                if (raw.StartsWith("Used when ")) kind = "Explicit";
                if (raw.StartsWith("Used automatically when ")) usageInstructions = true;
            }
            if (raw.StartsWith("Can only be equipped if ", StringComparison.OrdinalIgnoreCase)) kind = "Description";
            if (raw.StartsWith("Place into an allocated Jewel Socket", StringComparison.OrdinalIgnoreCase)) jewelInstructions = true;
            if (jewelInstructions) kind = "Description";
            if (essence && metadata == null && kind != "State" && !raw.StartsWith("Stack Size:", StringComparison.Ordinal))
                kind = raw.StartsWith("Right click", StringComparison.OrdinalIgnoreCase) ? "Instructions" : "Explicit";
            if (metadata == null && (raw.StartsWith("Right click this item", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("Can be used in a personal Map Device", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("Place into an allocated", StringComparison.OrdinalIgnoreCase))) usageInstructions = true;
            if (usageInstructions && !jewelInstructions) kind = "Instructions";
            if (ItemPresentation.IsClassLabel(raw)) kind = "Class";
            if (gem && metadata == null)
            {
                if (raw.StartsWith("Supports ", StringComparison.Ordinal)) gemSection = "Gem description";
                else if (raw.StartsWith("Place into ", StringComparison.Ordinal) || raw.StartsWith("Right click ",StringComparison.Ordinal)) gemSection = "Instructions";
                else if (raw.StartsWith("Support,",StringComparison.Ordinal) || raw.StartsWith("Lineage,",StringComparison.Ordinal) || raw == "Support") gemSection = "Gem tags";
                else if (kind == "Property" || kind == "State") gemSection = null;
                else if (gemSection == null && gemEffectSeen && !Regex.IsMatch(raw, @"\d")) gemSection = "Flavour";
                if (gemSection != null) kind = gemSection;
                else if (kind == "Item text" && Regex.IsMatch(raw, @"\d")) gemEffectSeen = true;
            }
            if(quest && kind is not ("Property" or "State"))
            {
                if(raw is "Quest Item" or "Quest Items") kind="Class";
                else { if(raw.StartsWith("Can be ",StringComparison.OrdinalIgnoreCase) || raw.StartsWith("Right click",StringComparison.OrdinalIgnoreCase) || raw.StartsWith("Take this",StringComparison.OrdinalIgnoreCase)) questInstructions=true; kind=questInstructions ? "Instructions" : "Flavour"; }
            }
            var unknownMarkers = Regex.Matches(raw, @"\(metadata:([^)]*)\)");
            var markers = Regex.Matches(raw, @"\((crafted|desecrated|fractured|mutated|vestigial|bonded|scourge|crucible|utility|cosmetic)\)", RegexOptions.IgnoreCase);
            string? lineMetadata = metadata?.Trim('{', ' ', '}');
            if (Regex.IsMatch(raw, @"\s+[—–-]\s+Unscalable Value$", RegexOptions.IgnoreCase))
            {
                raw = Regex.Replace(raw, @"\s+[—–-]\s+Unscalable Value$", "", RegexOptions.IgnoreCase);
                lineMetadata = (lineMetadata ?? "") + " · Unscalable Value";
            }
            if (markers.Count > 0) lineMetadata = string.Join(" · ", new[] { lineMetadata }.Concat(markers.Select(m => m.Groups[1].Value + " Modifier")).Where(v => !string.IsNullOrWhiteSpace(v)));
            if (unknownMarkers.Count > 0) lineMetadata = (lineMetadata ?? "") + " · Unknown metadata: " + string.Join(", ",unknownMarkers.Select(m => m.Groups[1].Value));
            var rolls = new List<ItemRoll>();
            string display = Range.Replace(raw, match =>
            {
                if (decimal.TryParse(match.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) &&
                    decimal.TryParse(match.Groups["min"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal min) &&
                    decimal.TryParse(match.Groups["max"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal max)) rolls.Add(new(value, min, max));
                return match.Groups["value"].Value;
            });
            display = FixedRoll.Replace(display, match =>
            {
                if (decimal.TryParse(match.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) &&
                    decimal.TryParse(match.Groups["reference"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var reference))
                    rolls.Add(new(value,reference,reference));
                return match.Groups["value"].Value;
            });
            display = Regex.Replace(display, @"\s*\((augmented|implicit|enchant|rune|crafted|desecrated|fractured|mutated|vestigial|bonded|scourge|crucible|utility|cosmetic)\)", "");
            display = Regex.Replace(display, @"\s*\(metadata:[^)]*\)", "");
            var tierMatch = TierPattern.Match(metadata ?? "");
            int? tier = tierMatch.Success && int.TryParse(tierMatch.Groups[1].Value, out int n) ? n : null;
            content.Add(new(display, kind, lineMetadata, tier, rolls));
        }
        content.AddRange(PseudoStats.Calculate(content));
        var speedLine = content.FirstOrDefault(l => l.Text.StartsWith("Attacks per Second:",StringComparison.Ordinal));
        if (speedLine != null && decimal.TryParse(speedLine.Text.Split(':')[1].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var speed))
        {
            decimal Damage(string prefix) => content.Where(l=>l.Text.StartsWith(prefix + ":",StringComparison.Ordinal))
                .SelectMany(l=>Regex.Matches(l.Text, @"(\d+(?:\.\d+)?)[-–](\d+(?:\.\d+)?)"))
                .Sum(m=>(decimal.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture)+decimal.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture))/2);
            decimal physical=Damage("Physical Damage")*speed, elemental=Damage("Elemental Damage")*speed, chaos=Damage("Chaos Damage")*speed;
            foreach(var pair in new[] { ("Physical DPS",physical), ("Elemental DPS",elemental), ("Total DPS",physical+elemental+chaos) })
                if(pair.Item2>0 && !content.Any(l=>l.Text.StartsWith(pair.Item1+":"))) content.Add(new(pair.Item1 + ": " + pair.Item2.ToString("0.##",CultureInfo.InvariantCulture),"Property"));
        }
        return new(Number(lines, "Item Level"), Number(lines, "Quality"), (lines.Contains("Corrupted") || lines.Contains("Twice Corrupted")),  lines.Contains("Unidentified"), content);
    }
}

public sealed record TradeFilters(string BaseType, bool MatchRarity = true, int? MinItemLevel = null, int? MinQuality = null, bool? Corrupted = null, int? MaxItemLevel = null, int? MaxQuality = null);
