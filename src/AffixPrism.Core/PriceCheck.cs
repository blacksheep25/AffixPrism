using System.Diagnostics;
using System.Text.Json;

namespace AffixPrism.Core;

public sealed record CheckHotkey(uint Modifiers, uint Key)
{
    public static CheckHotkey Default => new(1, 0x45);
    public string Label => string.Join("+", new[] { (Modifiers & 2) != 0 ? "Ctrl" : null, (Modifiers & 1) != 0 ? "Alt" : null, (Modifiers & 4) != 0 ? "Shift" : null,
        Key >= 0x70 ? $"F{Key - 0x6F}" : ((char)Key).ToString() }.Where(x => x != null));
    public static CheckHotkey? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        uint modifiers = 0;
        foreach (var part in parts[..^1])
        {
            uint bit = part.ToUpperInvariant() switch { "CTRL" or "CONTROL" => 2u, "ALT" => 1u, "SHIFT" => 4u, _ => 0u };
            if (bit == 0 || (modifiers & bit) != 0) return null;
            modifiers |= bit;
        }
        var keyText = parts[^1].ToUpperInvariant();
        uint key = keyText.Length == 1 && char.IsAsciiLetterOrDigit(keyText[0]) ? keyText[0] :
            keyText.StartsWith('F') && int.TryParse(keyText[1..], out int f) && f is >= 1 and <= 12 ? (uint)(0x6F + f) : 0;
        if (key == 0 || modifiers == 0 || (key == 0x43 && modifiers == 2) || (modifiers == 3 && key is 0x4F or 0x4C)) return null;
        return new(modifiers, key);
    }
}

public interface IItemCapturePlatform
{
    nint ForegroundGame { get; }
    bool ShortcutKeysReleased { get; }
    uint ClipboardVersion { get; }
    Task<bool> SendCopyAsync(CancellationToken cancellationToken);
    string? ReadItemText(nint gameWindow);
}

public sealed record CaptureResult(CopiedItem? Item, string? Error, string Stage = "capture");
public sealed class ItemCapture(IItemCapturePlatform platform)
{
    // The caller invokes this only for a user-pressed shortcut, never for log events.
    public async Task<CaptureResult> CaptureAsync(CancellationToken cancellationToken, int timeoutMs = 4000, int pollMs = 25)
    {
        nint game = platform.ForegroundGame;
        if (game == 0) return new(null, "Hover an item in POE2, then press your item-check shortcut.", "game-focus");
        var deadline = Stopwatch.StartNew();
        while (!platform.ShortcutKeysReleased)
        {
            if (platform.ForegroundGame != game) return new(null, "Item check cancelled because focus left POE2.");
            if (deadline.ElapsedMilliseconds >= timeoutMs) return new(null, "Release the shortcut keys, then try again.", "shortcut-release");
            await Task.Delay(pollMs, cancellationToken);
        }
        if (platform.ForegroundGame != game) return new(null, "Item check cancelled because focus left POE2.");
        uint before = platform.ClipboardVersion;
        if (!await platform.SendCopyAsync(cancellationToken)) return new(null, "The copy shortcut could not finish. Keep POE2 focused and run both apps at the same permission level.", "copy-input");
        deadline.Restart();
        bool changed = false;
        while (deadline.ElapsedMilliseconds < timeoutMs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (platform.ForegroundGame != game) return new(null, "Item check cancelled because focus left POE2.");
            if (platform.ClipboardVersion != before)
            {
                changed = true;
                string? text = platform.ReadItemText(game);
                if (text != null)
                {
                    var item = ItemParser.Parse(text);
                    return item != null ? new(item, null, "complete") : new(null, "Fresh clipboard text isn't a supported item. Hover the item and retry; English text is required.", "item-format");
                }
            }
            await Task.Delay(pollMs, cancellationToken);
        }
        return changed ? new(null, "The clipboard changed but stayed busy. Close any clipboard utility and retry.", "clipboard-busy") : new(null, "POE2 did not copy an item. Keep the cursor on the item until the check finishes.", "clipboard-unchanged");
    }
}

public interface ICopyKeySender
{
    bool HasGameFocus { get; }
    bool SendKey(ushort virtualKey, bool down);
}

public static class CopyChord
{
    public static async Task<bool> SendAsync(ICopyKeySender sender, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!sender.HasGameFocus) return false;
        bool ctrl = false, copy = false, success = false;
        try
        {
            ctrl = sender.SendKey(0x11, true);
            if (!ctrl) return false;
            // Separate key transitions across game frames; a down/up in a single
            // SendInput batch can be missed by games that poll keyboard state.
            await Task.Delay(35, cancellationToken);
            if (!sender.HasGameFocus) return false;
            copy = sender.SendKey(0x43, true);
            if (!copy) return false;
            await Task.Delay(90, cancellationToken);
            success = true;
        }
        finally
        {
            // Always release synthetic keys, including on cancellation/focus loss.
            if (copy) success &= sender.SendKey(0x43, false);
            if (ctrl) success &= sender.SendKey(0x11, false);
        }
        return success;
    }
}

public static class TradeSearch
{
    public static string BuildUrl(CopiedItem item, string league, TradeFilters? filters = null)
    {
        if (string.IsNullOrWhiteSpace(league)) throw new ArgumentException("Set your league in Settings before searching.");
        string baseType = filters?.BaseType.Trim() ?? item.BaseType;
        if (string.IsNullOrWhiteSpace(baseType)) throw new ArgumentException("Enter a base type for the trade search.");
        if (filters?.MinItemLevel is < 0 or > 100 || filters?.MinQuality is < 0 or > 100) throw new ArgumentException("Level and quality filters must be between 0 and 100.");
        if (filters?.MaxItemLevel is < 0 or > 100 || filters?.MaxQuality is < 0 or > 100) throw new ArgumentException("Level and quality maximums must be between 0 and 100.");
        if (filters?.MinItemLevel > filters?.MaxItemLevel || filters?.MinQuality > filters?.MaxQuality) throw new ArgumentException("A minimum cannot exceed its maximum.");
        var query = new Dictionary<string, object> { ["status"] = new { option = "online" } };
        bool unidentified = ItemAnalysis.From(item).Unidentified;
        if (item.Rarity == "Unique" && !unidentified) { query["name"] = item.Name; query["type"] = baseType; }
        else if (!unidentified && !BaseTypes.IsKnown(baseType) && item.Rarity == "Magic" && item.Name == item.BaseType && (filters == null || baseType == item.Name))
            throw new ArgumentException("This magic item's base type could not be identified reliably. Copy an item with a separate base-type line.");
        else query["type"] = baseType;
        var groups = new Dictionary<string, object>();
        var typeFilters = new Dictionary<string, object>();
        if ((filters?.MatchRarity ?? true) && item.Rarity is "Normal" or "Magic" or "Rare" or "Unique")
            typeFilters["rarity"] = new { option = item.Rarity.ToLowerInvariant() };
        var misc = new Dictionary<string, object> { ["identified"] = new { option = unidentified ? "false" : "true" } };
        static Dictionary<string, int> Bounds(int? min, int? max)
        {
            var bounds = new Dictionary<string, int>();
            if (min.HasValue) bounds["min"] = min.Value;
            if (max.HasValue) bounds["max"] = max.Value;
            return bounds;
        }
        if (filters?.MinItemLevel != null || filters?.MaxItemLevel != null) typeFilters["ilvl"] = Bounds(filters?.MinItemLevel, filters?.MaxItemLevel);
        if (filters?.MinQuality != null || filters?.MaxQuality != null) typeFilters["quality"] = Bounds(filters?.MinQuality, filters?.MaxQuality);
        if (typeFilters.Count > 0) groups["type_filters"] = new { filters = typeFilters };
        if (filters?.Corrupted is { } corrupted) misc["corrupted"] = new { option = corrupted.ToString().ToLowerInvariant() };
        if (misc.Count > 0) groups["misc_filters"] = new { filters = misc };
        if (groups.Count > 0) query["filters"] = groups;
        var json = JsonSerializer.Serialize(new { query, sort = new { price = "asc" } });
        return "https://www.pathofexile.com/trade2/search/poe2/" + Uri.EscapeDataString(league.Trim()) + "?q=" + Uri.EscapeDataString(json);
    }
}
