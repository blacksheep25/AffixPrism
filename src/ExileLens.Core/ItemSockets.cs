using System.Text.Json;
using System.Text.RegularExpressions;
namespace ExileLens.Core;
public static class ItemSockets
{
    public static IReadOnlyList<ItemSocket> From(CopiedItem item)
    {
        if (item.Sockets != null) return item.Sockets;
        var match = Regex.Match(item.Details, @"(?m)^Sockets:\s*([^\r\n]+)");
        if (!match.Success) return Array.Empty<ItemSocket>();
        int count = match.Groups[1].Value.Split(new[] { ' ', '-' },StringSplitOptions.RemoveEmptyEntries).Length;
        var runes = ItemAnalysis.From(item).Lines.Where(l=>l.Kind == "Rune").ToArray();
        if(runes.Length==0)
            return Enumerable.Range(0,Math.Min(count,12)).Select(i=>new ItemSocket(i,"rune",null,null,false,"No socket effects in copied item text.",true)).ToArray();
        // Clipboard effects can be totals across sockets, not one line per socket.
        if (count == 2 && runes.Length == 1 && runes[0].Text == "36% increased Armour, Evasion and Energy Shield")
            return Enumerable.Range(0, 2).Select(i => new ItemSocket(i, "rune", "Iron Rune family", null, true,
                "Combined effect: " + runes[0].Text + "\n\nPossible combinations:\n2 × Greater Iron Rune (18% + 18%)\nIron Rune + Perfect Iron Rune (16% + 20%)\n\nCopied text does not identify the individual tiers or socket order.", true)).ToArray();
        return Enumerable.Range(0,Math.Min(count,12)).Select(i => {
            if (runes.Length != count) return new ItemSocket(i,"socket",null,null,null,
                runes.Length == 0 ? null : "Combined socket effects:\n"+string.Join("\n",runes.Select(r=>r.Text))+"\n\nCopied text does not identify each socket separately.", true);
            string effect=runes[i].Text;
            string? name = effect switch {
                "+6 to Dexterity" => "Lesser Adept Rune",
                "+9 to Dexterity" => "Adept Rune",
                "+12 to Dexterity" => "Greater Adept Rune",
                "+15 to Dexterity" => "Perfect Adept Rune",
                "14% increased Armour, Evasion and Energy Shield" => "Lesser Iron Rune",
                "16% increased Armour, Evasion and Energy Shield" => "Iron Rune",
                "18% increased Armour, Evasion and Energy Shield" => "Greater Iron Rune",
                "20% increased Armour, Evasion and Energy Shield" => "Perfect Iron Rune",
                "Adds 1 to 30 Lightning Damage" => "Greater Storm Rune",
                "Attacks with this Weapon Penetrate 25% Elemental Resistances" => "Soul Core of Topotante",
                _ => null
            };
            if (item.ItemClass == "Bows") name ??= effect switch {
                "18% increased Physical Damage" => "Greater Iron Rune",
                "Bow Attacks fire an additional Arrow" => "Countess Seske's Rune of Archery",
                _ => null
            };
            name=SocketAugments.Match(item.ItemClass,effect) ?? name;
            return new ItemSocket(i,"rune",name,null,true,effect,true);
        }).ToArray();
    }
    public static IReadOnlyList<ItemSocket>? Parse(JsonElement item)
    {
        if (!item.TryGetProperty("sockets",out var sockets) || sockets.ValueKind != JsonValueKind.Array) return null;
        string? Text(JsonElement el,string key) => el.TryGetProperty(key,out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        var children = item.TryGetProperty("socketedItems",out var socketed) && socketed.ValueKind == JsonValueKind.Array ? socketed.EnumerateArray().ToArray() : Array.Empty<JsonElement>();
        var result = new List<ItemSocket>();
        int index=0;
        foreach(var socket in sockets.EnumerateArray().Take(12))
        {
            if (socket.ValueKind != JsonValueKind.Object) { index++; continue; }
            var child = children.FirstOrDefault(c=>c.ValueKind == JsonValueKind.Object && c.TryGetProperty("socket",out var n) && n.ValueKind == JsonValueKind.Number && n.TryGetInt32(out var i) && i==index);
            bool found=child.ValueKind == JsonValueKind.Object;
            string? name = found ? Text(child,"name") : null;
            if (found && string.IsNullOrWhiteSpace(name)) name=Text(child,"typeLine") ?? Text(child,"baseType");
            string? category=Text(socket,"item");
            string? icon=found ? ItemArtwork.SafeUrl(Text(child,"socketedIcon")) ?? ItemArtwork.SafeUrl(Text(child,"icon")) : null;
            result.Add(new(index,Text(socket,"type") ?? "socket",name ?? category,icon,found || category != null ? true : socketed.ValueKind == JsonValueKind.Array ? false : null));
            index++;
        }
        return result;
    }
}
