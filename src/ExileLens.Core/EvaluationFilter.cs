using System.Globalization;
using System.Text.RegularExpressions;
namespace ExileLens.Core;

public sealed class EvaluationFilter
{
    public string Text { get; }
    public int GroupId { get; }
    public int ValueIndex { get; }
    public string Kind { get; }
    public decimal? CopiedValue { get; }
    public bool Enabled { get; set; }
    public bool Unscalable { get; }
    public bool WholeNumber => Regex.IsMatch(Text, @"(?i)to level of .*skills|^(?:Item Level|Rune Sockets|Sockets):|^Requires:|to (?:Strength|Dexterity|Intelligence|all Attributes|Accuracy Rating)|to maximum (?:Life|Mana)$");
    public string Minimum { get; set; } = "";
    public string Maximum { get; set; } = "";
    // Broad matching is for variable performance rolls, not eligibility or discrete mechanics.
    public bool AllowsBroad => !Unscalable && CopiedValue > 0 && !Regex.IsMatch(Text,
        @"(?i)^(?:Requires:|Item Level:|Level:|Quality:|Sockets:|Rune Sockets:|Stack Size:)|to level of .*skills|(?:additional|maximum) (?:arrows?|projectiles?|charges?|sockets?)|(?:arrows?|projectiles?) (?:additional|fired)|uses remaining");
    public EvaluationFilter(ItemLine line, int valueIndex = 0, int groupId = 0)
    {
        Text = line.Text; Kind = line.Kind; ValueIndex = valueIndex; GroupId = groupId;
        Unscalable = line.Metadata?.Contains("Unscalable Value", StringComparison.OrdinalIgnoreCase) == true;
        var matches = Regex.Matches(line.Text, @"(?<![\d.])[+-]?\d+(?:\.\d+)?");
        var match = valueIndex >= 0 && valueIndex < matches.Count ? matches[valueIndex] : Match.Empty;
        CopiedValue = match.Success && decimal.TryParse(match.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal n) ? n : null;
    }
    public void Preset(bool broad)
    {
        if (CopiedValue is not { } value) return;
        decimal minimum = broad && AllowsBroad ? value - Math.Abs(value) * .1m : value;
        // Round up to retain the same minimum constraint for discrete stats.
        if (WholeNumber) minimum = decimal.Ceiling(minimum);
        Minimum = minimum.ToString("0.##", CultureInfo.InvariantCulture);
        Maximum = "";
    }
    public string? Validate()
    {
        if (!Enabled) return null;
        decimal? min = null, max = null;
        if (!string.IsNullOrWhiteSpace(Minimum))
        {
            if (!decimal.TryParse(Minimum, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal n)) return "Minimum must be a number.";
            min = n;
        }
        if (!string.IsNullOrWhiteSpace(Maximum))
        {
            if (!decimal.TryParse(Maximum, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal n)) return "Maximum must be a number.";
            max = n;
        }
        if (WholeNumber && (min.HasValue && min.Value != decimal.Truncate(min.Value) || max.HasValue && max.Value != decimal.Truncate(max.Value))) return "This stat requires whole-number bounds.";
        return min > max ? "Minimum cannot exceed maximum." : null;
    }
}
