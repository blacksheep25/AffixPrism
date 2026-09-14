using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ExileLens.Core;

// Website search adapter. No background collection, cookies from other apps, or automatic retries.
public sealed class LiveTradeClient(HttpClient http)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, DateTimeOffset> nextRequests = new();
    private JsonElement? statCatalog, filterCatalog;
    private DateTimeOffset catalogExpires;
    private readonly Dictionary<string, (DateTimeOffset At, ComparableResult Result)> cache = new();
    public async Task<ComparableResult> SearchAsync(ComparableRequest request, CancellationToken token, Action<string>? progress = null)
    {
        await gate.WaitAsync(token);
        try
        {
            string cacheKey = request.League + "\n" + JsonSerializer.Serialize(request);
            if (cache.TryGetValue(cacheKey, out var saved) && DateTimeOffset.UtcNow - saved.At < TimeSpan.FromMinutes(2))
                return saved.Result with { Source = saved.Result.Source + " · cached (under 2 minutes)" };
            if(DateTimeOffset.UtcNow>=catalogExpires) { statCatalog=null; filterCatalog=null; catalogExpires=DateTimeOffset.UtcNow.AddMinutes(30); }
            if (request.Filters.Any(x => x.Kind != "Property" || x.Text.StartsWith("Grants Skill:", StringComparison.Ordinal)) && statCatalog == null)
                statCatalog = await SendAsync(HttpMethod.Get, "/api/trade2/data/stats", null, "stats", "Loading trade filters…", token, progress);
            string? category = null;
            if (!request.ExactBase)
            {
                filterCatalog ??= await SendAsync(HttpMethod.Get, "/api/trade2/data/filters", null, "filters", "Loading item categories…", token, progress);
                category = ResolveCategory(filterCatalog.Value, request.Item.ItemClass);
            }
            string body = BuildQuery(request, statCatalog, category);
            var search = await SendAsync(HttpMethod.Post, "/api/trade2/search/poe2/" + Uri.EscapeDataString(request.League), body, "search", "Searching POE trade…", token, progress);
            string queryId = ReadText(search.GetProperty("id"), "search.id");
            int? total = search.TryGetProperty("total", out var count) && count.ValueKind==JsonValueKind.Number && count.TryGetInt32(out var found) ? found : null;
            ComparableResult Complete(ComparableResult result, int fetched)
            {
                result = result with { TotalMatches = total, FetchedCount = fetched, SearchUrl = "https://www.pathofexile.com/trade2/search/poe2/" + Uri.EscapeDataString(request.League) + "/" + Uri.EscapeDataString(queryId) };
                if (cache.Count >= 30) cache.Clear();
                cache[cacheKey] = (DateTimeOffset.UtcNow, result);
                return result;
            }
            var page = search.GetProperty("result").EnumerateArray().Take(30).ToArray();
            // Search can return IDs or object entries. Complete listings need no second request.
            if (page.Length > 0 && page.All(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("listing", out _) && x.TryGetProperty("item", out _)))
                return Complete(ParseListings(search, request, DateTimeOffset.UtcNow), page.Length);
            var ids = page.Select((x, i) => ReadText(x.ValueKind == JsonValueKind.Object && x.TryGetProperty("id", out var id) ? id : x, $"search.result[{i}].id")).Distinct().ToArray();
            if (ids.Length == 0) return Complete(Empty(request), 0);
            var combined = new System.Text.Json.Nodes.JsonArray();
            int pageNumber=0;
            foreach (var batch in ids.Chunk(10))
            {
                if (combined.Count > 0)
                {
                    using var partialDoc=JsonDocument.Parse(new System.Text.Json.Nodes.JsonObject { ["result"]=combined.DeepClone() }.ToJsonString());
                    var partial=ParseListings(partialDoc.RootElement,request,DateTimeOffset.UtcNow);
                    if (partial.Rows.Count >= 10 || nextRequests.GetValueOrDefault("fetch") - DateTimeOffset.UtcNow > TimeSpan.FromSeconds(10))
                        return Complete(partial with { Source=partial.Source + " · additional pages not fetched" },combined.Count);
                }
                pageNumber++;
                var fetched = await SendAsync(HttpMethod.Get, "/api/trade2/fetch/" + string.Join(",", batch.Select(Uri.EscapeDataString)) + "?query=" + Uri.EscapeDataString(queryId), null, "fetch", $"Loading listing page {pageNumber} of {(ids.Length+9)/10}", token, progress);
                foreach(var row in fetched.GetProperty("result").EnumerateArray()) combined.Add(System.Text.Json.Nodes.JsonNode.Parse(row.GetRawText()));
            }
            progress?.Invoke("Checking listing filters…");
            using var combinedDoc=JsonDocument.Parse(new System.Text.Json.Nodes.JsonObject { ["result"]=combined }.ToJsonString());
            return Complete(ParseListings(combinedDoc.RootElement, request, DateTimeOffset.UtcNow), combined.Count);
        }
        finally { gate.Release(); }
    }
    private static ComparableResult Empty(ComparableRequest request) => new([], null, null, null, 0, request.Currency, "POE trade · first 30 offers", DateTimeOffset.UtcNow);
    private async Task<JsonElement> SendAsync(HttpMethod method, string path, string? body, string policy, string stage, CancellationToken token, Action<string>? progress)
    {
        var delay = nextRequests.GetValueOrDefault(policy) - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.FromSeconds(10)) throw new HttpRequestException($"Trade is cooling down. Retry in {Math.Ceiling(delay.TotalSeconds)} seconds.");
        if (delay > TimeSpan.Zero)
        {
            var until=nextRequests.GetValueOrDefault(policy);
            while((delay=until-DateTimeOffset.UtcNow)>TimeSpan.Zero)
            {
                progress?.Invoke($"{stage} · ready in {Math.Ceiling(delay.TotalSeconds)}s (trade request spacing)");
                await Task.Delay(delay<TimeSpan.FromSeconds(1) ? delay : TimeSpan.FromSeconds(1), token);
            }
        }
        // Independent endpoint budgets: a search must not delay its first listing fetch.
        nextRequests[policy] = DateTimeOffset.UtcNow.AddSeconds(4);
        progress?.Invoke(stage);
        using var message = new HttpRequestMessage(method, "https://www.pathofexile.com" + path);
        message.Headers.UserAgent.ParseAdd("ExileLens/0.3");
        message.Headers.Accept.ParseAdd("application/json");
        if (body != null) message.Content = new StringContent(body, Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        try { response = await http.SendAsync(message, token); }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException { SocketErrorCode: System.Net.Sockets.SocketError.AccessDenied })
        { throw new HttpRequestException("Windows blocked ExileLens's connection to pathofexile.com. Check the app's network permissions.", ex); }
        using (response)
        {
            nextRequests[policy] = NextRequest(response, DateTimeOffset.UtcNow, nextRequests[policy]);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new HttpRequestException($"Trade rate limit reached. Retry in {Math.Ceiling((nextRequests[policy] - DateTimeOffset.UtcNow).TotalSeconds)} seconds.");
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                throw new HttpRequestException("The trade site requires sign-in or browser verification. ExileLens's direct connection cannot complete that verification yet.");
            if(response.StatusCode==HttpStatusCode.BadRequest)
            {
                statCatalog=null; filterCatalog=null; cache.Clear();
                throw new HttpRequestException("Trade rejected the query (HTTP 400). Check league and filters; the filter catalogue will reload on your next Search. No automatic retry was sent.");
            }
            if((int)response.StatusCode>=500) throw new HttpRequestException($"Trade service unavailable (HTTP {(int)response.StatusCode}). Try again later; no automatic retry was sent.");
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Trade search failed (HTTP {(int)response.StatusCode}). Check the league and selected filters.");
            JsonDocument document;
            try { document=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token)); }
            catch(JsonException ex) { throw new JsonException("Trade returned an invalid JSON response. The service may be unavailable or require browser verification.",ex); }
            using var ownedDocument=document;
            if(document.RootElement.ValueKind!=JsonValueKind.Object) throw new JsonException("Unsupported trade response: expected a JSON object. No prices were inferred.");
            if (document.RootElement.TryGetProperty("error", out _)) throw new HttpRequestException("The trade site rejected this query. Check the selected filters.");
            return document.RootElement.Clone();
        }
    }
    // A valid server budget replaces the fallback spacing. Requests remain serialized.
    public static DateTimeOffset NextRequest(HttpResponseMessage response, DateTimeOffset now, DateTimeOffset fallback)
    {
        bool hasBudget = false;
        foreach (var header in response.Headers.Where(h => h.Key.StartsWith("X-Rate-Limit-", StringComparison.OrdinalIgnoreCase) && !h.Key.EndsWith("-State", StringComparison.OrdinalIgnoreCase)))
        {
            if (!response.Headers.TryGetValues(header.Key + "-State", out var states)) continue;
            var rules = string.Join(",",header.Value).Split(',');
            var counters = string.Join(",",states).Split(',');
            if (rules.Length != counters.Length) return Cooldown(response,now,fallback);
            for (int i=0;i<rules.Length;i++)
            {
                var r=rules[i].Split(':'); var c=counters[i].Split(':');
                if (r.Length!=3 || c.Length!=3 || !int.TryParse(r[0],out var limit) || limit<=0 || !int.TryParse(r[1],out var window) || window<=0 || !int.TryParse(r[2],out var penalty) || penalty<0 || !int.TryParse(c[0],out var used) || used<0 || !int.TryParse(c[1],out var stateWindow) || stateWindow!=window || !int.TryParse(c[2],out var ban) || ban<0)
                    return Cooldown(response,now,fallback);
                hasBudget=true;
            }
        }
        return Cooldown(response,now,hasBudget ? now : fallback);
    }
    public static DateTimeOffset Cooldown(HttpResponseMessage response, DateTimeOffset now, DateTimeOffset minimum)
    {
        var until = minimum;
        foreach (var header in response.Headers.Where(x => x.Key.StartsWith("X-Rate-Limit-", StringComparison.OrdinalIgnoreCase) && !x.Key.EndsWith("-State", StringComparison.OrdinalIgnoreCase)))
        {
            if (!response.Headers.TryGetValues(header.Key + "-State", out var states)) continue;
            var rules = string.Join(",", header.Value).Split(',');
            var counters = string.Join(",", states).Split(',');
            for (int i = 0; i < Math.Min(rules.Length, counters.Length); i++)
            {
                var rule = rules[i].Split(':'); var state = counters[i].Split(':');
                if (rule.Length < 2 || state.Length < 3 || !int.TryParse(rule[0], out var limit) || !int.TryParse(rule[1], out var window) || !int.TryParse(state[0], out var used) || !int.TryParse(state[2], out var ban)) continue;
                int seconds = Math.Max(ban, used >= Math.Max(1, limit - 1) ? window : 0);
                if (seconds > 0 && now.AddSeconds(seconds) > until) until = now.AddSeconds(seconds);
            }
        }
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retry = response.Headers.RetryAfter?.Date ?? now + (response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(5));
            if (retry > until) until = retry;
        }
        return until;
    }
    public static string CurrencyId(string currency) => currency switch
    {
        "Exalted Orb" => "exalted", "Divine Orb" => "divine", "Chaos Orb" => "chaos",
        _ => throw new ArgumentException("Live search supports Exalted Orb, Divine Orb and Chaos Orb. Choose one in the price window.")
    };
    private static string Signature(string text) => Regex.Replace(ComparableMarket.Signature(text).Replace("+#", "#"), @"\s+", " ");
    private static string ReadText(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.String) return value.GetString()!;
        if (value.ValueKind == JsonValueKind.Null) return "";
        throw new JsonException($"Unsupported trade response at {path}: expected text, received {value.ValueKind}. Please report this field name.");
    }
    private static string ReadModifier(JsonElement value, string path)
    {
        // GGG ItemMod: description plus optional flags. Legacy listings used strings.
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("description", out var description))
        {
            string text = ReadText(description, path + ".description");
            if (value.TryGetProperty("flags",out var flags) && flags.ValueKind == JsonValueKind.Object)
            {
                string suffix = string.Concat(flags.EnumerateObject().Where(f => f.Value.ValueKind == JsonValueKind.True).Select(f => new[] { "fractured", "crafted", "desecrated", "mutated", "vestigial" }.Contains(f.Name) ? " (" + f.Name + ")" : " (metadata:" + f.Name + ")"));
                text = string.Join("\n",text.Replace("\r","").Split('\n').Select(line => line + suffix));
            }
            return text;
        }
        return ReadText(value, path);
    }
    public static string ResolveCategory(JsonElement catalog, string itemClass)
    {
        string Normalize(string text) => text.ToLowerInvariant().Replace("quarterstaves", "quarterstaff").Replace("staves", "staff").TrimEnd('s');
        var options = catalog.GetProperty("result").EnumerateArray()
            .SelectMany(g => g.GetProperty("filters").EnumerateArray())
            .Where(f => f.GetProperty("id").GetString() == "category")
            .SelectMany(f => f.GetProperty("option").GetProperty("options").EnumerateArray());
        var matches = options.Where(o => o.TryGetProperty("text", out var text) && Normalize(text.GetString() ?? "") == Normalize(itemClass))
            .Select(o => o.GetProperty("id").GetString()).Where(id=>!string.IsNullOrWhiteSpace(id)).Distinct().ToArray();
        return matches.Length == 1 ? matches[0]! : throw new ArgumentException("Cannot resolve this item's trade category. Enable the base-name filter to search its exact base.");
    }
    public static string BuildQuery(ComparableRequest request, JsonElement? catalog, string? category = null)
    {
        if (string.IsNullOrWhiteSpace(request.League)) throw new ArgumentException("Select your league in Settings before searching.");
        if (request.ExactBase && request.Item.Rarity == "Magic" && request.Item.BaseType == request.Item.Name && !BaseTypes.IsKnown(request.Item.BaseType) && !ItemAnalysis.From(request.Item).Unidentified) throw new ArgumentException("Cannot identify this magic item's base type reliably.");
        if (request.Filters.Any(x => x.Minimum > x.Maximum || x.ValueIndex < 0)) throw new ArgumentException("Invalid filter bounds.");
        var analysis = ItemAnalysis.From(request.Item);
        var type = new Dictionary<string, object> { ["rarity"] = new { option = request.Item.Rarity.ToLowerInvariant() } };
        if (!request.ExactBase && category != null) type["category"] = new { option = category };
        var equipment = new Dictionary<string, object>();
        var misc = new Dictionary<string, object> { ["corrupted"] = new { option = analysis.Corrupted ? "true" : "false" }, ["identified"] = new { option = analysis.Unidentified ? "false" : "true" } };
        var trade = new Dictionary<string, object> { ["collapse"] = new { option = "true" } };
        if (request.Currency != "Auto") trade["price"] = new { option = CurrencyId(request.Currency) };
        if (request.MaximumAgeDays is { } days) trade["indexed"] = new { option = days switch { 1 => "1day", 3 => "3days", 7 => "1week", _ => throw new ArgumentException("Unsupported listing age.") } };
        var stats = new List<object>();
        var alternatives = new List<object>();
        foreach (var group in request.Filters.GroupBy(x => (x.GroupId, x.Text, x.Kind)))
        {
            var first = group.First();
            var range = new Dictionary<string, decimal>();
            if (first.Minimum.HasValue) range["min"] = first.Minimum.Value;
            if (first.Maximum.HasValue) range["max"] = first.Maximum.Value;
            bool grantedSkill = first.Text.StartsWith("Grants Skill:", StringComparison.Ordinal);
            if (first.Kind == "Property" && !grantedSkill)
            {
                // Multi-component properties are checked against returned item text, not an averaged server stat.
                var name = first.Text.Split(':')[0];
                if (group.Count() > 1) continue;
                switch (name)
                {
                    case "Physical DPS": equipment["pdps"] = range; break;
                    case "Elemental DPS": equipment["edps"] = range; break;
                    case "Total DPS": equipment["dps"] = range; break;
                    case "Attacks per Second": equipment["aps"] = range; break;
                    case "Critical Hit Chance": equipment["crit"] = range; break;
                    case "Item Level": type["ilvl"] = range; break;
                    case "Quality": type["quality"] = range; break;
                    case "Armour": equipment["ar"] = range; break;
                    case "Evasion Rating": equipment["ev"] = range; break;
                    case "Energy Shield": equipment["es"] = range; break;
                    case "Rune Sockets": equipment["rune_sockets"] = range; break;
                    // Other properties remain mandatory client-side filters over the fetched page.
                }
                continue;
            }
            string kind = grantedSkill ? "skill" : first.Kind switch { "Pseudo" => "pseudo", "Implicit" => "implicit", "Rune" => "rune", "Enchant" => "enchant", _ => "explicit" };
            var candidates = catalog?.GetProperty("result").EnumerateArray().SelectMany(x => x.GetProperty("entries").EnumerateArray())
                .Where(x => ReadText(x.GetProperty("id"), "stats.entries.id").StartsWith(kind + ".", StringComparison.Ordinal) && !x.TryGetProperty("option", out _) && Signature(ReadText(x.GetProperty("text"), "stats.entries.text")) == Signature(first.Text))
                .Select(x => ReadText(x.GetProperty("id"), "stats.entries.id")).Distinct().ToArray() ?? [];
            if (candidates.Length == 0) throw new ArgumentException($"Cannot map this selected filter to a trade stat: {first.Text}. Deselect it to search more broadly.");
            // The trade site uses one magnitude for multi-number stats; enforce individual values locally.
            bool single = Regex.Matches(first.Text, @"(?<![\d.])[+-]?\d+(?:\.\d+)?").Count == 1 && first.ValueIndex == 0;
            if(candidates.Length==1) stats.Add(new { id = candidates[0], value = single ? range : new Dictionary<string, decimal>() });
            else
                // Identical text can have separate IDs for different item contexts (e.g. Spirit).
                // Require at least one matching ID at the same bounds, not all IDs or a guessed one.
                alternatives.Add(new { type="count", value=new { min=1 }, filters=candidates.Select(id=>new { id, value=single ? range : new Dictionary<string,decimal>() }).ToArray() });
        }
        var filters = new Dictionary<string, object> { ["type_filters"] = new { filters = type }, ["misc_filters"] = new { filters = misc }, ["trade_filters"] = new { filters = trade } };
        if (equipment.Count > 0) filters["equipment_filters"] = new { filters = equipment };
        var statGroups=new List<object> { new { type="and", filters=stats } }; statGroups.AddRange(alternatives);
        var query = new Dictionary<string, object> { ["status"] = new { option = request.InstantBuyOnly ? "securable" : request.OnlineOnly ? "online" : "any" }, ["filters"] = filters, ["stats"] = statGroups };
        if (request.ExactBase) query["type"] = request.Item.BaseType;
        if (request.Item.Rarity == "Unique" && !analysis.Unidentified) query["name"] = request.Item.Name;
        return JsonSerializer.Serialize(new { query, sort = new { price = "asc" } });
    }
    public static ComparableResult ParseListings(JsonElement response, ComparableRequest request, DateTimeOffset now)
    {
        var listings = new List<ImportedListing>();
        foreach (var row in response.GetProperty("result").EnumerateArray().Take(30))
        {
            if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("listing", out var listing) || !listing.TryGetProperty("price", out var price) || !price.TryGetProperty("amount", out var amount) || amount.ValueKind != JsonValueKind.Number || !amount.TryGetDecimal(out var value) || value <= 0 || (request.Currency != "Auto" && ReadText(price.GetProperty("currency"), "listing.price.currency") != CurrencyId(request.Currency))) continue;
            if (!listing.TryGetProperty("indexed", out var indexed) || !indexed.TryGetDateTimeOffset(out var date) || date > now.AddMinutes(5)) continue;
            var item = row.GetProperty("item");
            string rarity = item.TryGetProperty("rarity", out var rarityValue) && rarityValue.ValueKind == JsonValueKind.String
                ? ReadText(rarityValue, "item.rarity")
                : item.TryGetProperty("frameType", out var frame) && frame.ValueKind == JsonValueKind.Number
                    ? frame.GetInt32() switch { 0 => "Normal", 1 => "Magic", 2 => "Rare", 3 => "Unique", _ => "Unknown" }
                    : throw new JsonException("Trade item has no supported rarity field.");
            string name = ReadText(item.GetProperty("name"), "item.name");
            string baseType = item.TryGetProperty("baseType", out var baseValue) ? ReadText(baseValue, "item.baseType") : ReadText(item.GetProperty("typeLine"), "item.typeLine");
            var text = new StringBuilder($"Item Class: {request.Item.ItemClass}\nRarity: {rarity}\n");
            if (name.Length > 0) text.AppendLine(name);
            text.AppendLine(baseType).AppendLine("--------");
            if (item.TryGetProperty("ilvl", out var ilvl)) text.AppendLine("Item Level: " + ilvl.GetInt32());
            var propertyLines = new HashSet<string>(StringComparer.Ordinal);
            foreach (string propertyField in new[] { "properties", "grantedSkills" })
            if (item.TryGetProperty(propertyField, out var properties) && properties.ValueKind == JsonValueKind.Array)
                foreach (var property in properties.EnumerateArray())
                {
                    string label = ReadText(property.GetProperty("name"), "item.properties.name");
                    string values = string.Join(", ", property.GetProperty("values").EnumerateArray().Select(x => ReadText(x[0], $"item.properties[{label}].values")));
                    string propertyText = ItemAnalysis.CleanTradeText(label + (values.Length > 0 ? ": " + values : ""));
                    if (propertyLines.Add(propertyText)) text.AppendLine(propertyText);
                }
            if (item.TryGetProperty("requirements", out var requirements) && requirements.ValueKind == JsonValueKind.Array)
            {
                var parts = new List<string>();
                foreach (var requirement in requirements.EnumerateArray())
                {
                    string label = ReadText(requirement.GetProperty("name"), "item.requirements.name");
                    string values = string.Join(", ", requirement.GetProperty("values").EnumerateArray().Select(x => ReadText(x[0], "item.requirements.values")));
                    parts.Add(label == "Level" ? "Level " + values : values + " " + label);
                }
                if (parts.Count > 0) text.AppendLine("Requires: " + string.Join(", ",parts));
            }
            if (item.TryGetProperty("sockets", out var sockets) && sockets.ValueKind == JsonValueKind.Array && sockets.GetArrayLength() > 0)
                text.AppendLine("Sockets: " + string.Join(" ", Enumerable.Repeat("S",sockets.GetArrayLength())));
            foreach (var (key, suffix) in new[] { ("implicitMods", " (implicit)"), ("enchantMods", " (enchant)"), ("runeMods", " (rune)"), ("explicitMods", ""), ("craftedMods", " (crafted)"), ("fracturedMods", " (fractured)"), ("desecratedMods", " (desecrated)"), ("bondedMods", " (bonded)"), ("utilityMods", " (utility)"), ("cosmeticMods", " (cosmetic)"), ("scourgeMods", " (scourge)"), ("crucibleMods", " (crucible)") })
                if (item.TryGetProperty(key, out var mods))
                    foreach (var mod in mods.EnumerateArray())
                        foreach (var line in ReadModifier(mod, "item." + key).Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
                            text.AppendLine(line + suffix);
            if ((item.TryGetProperty("doubleCorrupted", out var twice) || item.TryGetProperty("twiceCorrupted", out twice)) && twice.ValueKind == JsonValueKind.True) text.AppendLine("Twice Corrupted");
            else if (item.TryGetProperty("corrupted", out var corrupted) && corrupted.ValueKind == JsonValueKind.True) text.AppendLine("Corrupted");
            if (item.TryGetProperty("identified", out var identified) && identified.ValueKind == JsonValueKind.False) text.AppendLine("Unidentified");
            foreach (var (flag,label) in new[] { ("desecrated","Desecrated"), ("fractured","Fractured Item"), ("sanctified","Sanctified"), ("duplicated","Mirrored"), ("split","Split"), ("unmodifiable","Unmodifiable"), ("unmodifiableExceptChaos","Unmodifiable except Chaos"), ("veiled","Veiled") })
                if (item.TryGetProperty(flag,out var state) && state.ValueKind == JsonValueKind.True) text.AppendLine(label);
            foreach (var field in new[] { "flavourText", "descrText", "secDescrText" })
                if (item.TryGetProperty(field, out var description))
                {
                    text.AppendLine("--------").AppendLine(field == "flavourText" ? "{ Flavour Text }" : "{ Description }");
                    if (description.ValueKind == JsonValueKind.Array) foreach (var part in description.EnumerateArray()) text.AppendLine(ReadText(part,"item."+field));
                    else if (description.ValueKind == JsonValueKind.String) text.AppendLine(description.GetString());
                    text.AppendLine("--------");
                }
            var account = listing.GetProperty("account");
            bool online = account.TryGetProperty("online", out var status) && status.ValueKind == JsonValueKind.Object;
            // securable is enforced by the search endpoint. Do not infer it from a price type.
            string? icon = item.TryGetProperty("icon", out var artwork) && artwork.ValueKind == JsonValueKind.String ? ItemArtwork.SafeUrl(artwork.GetString()) : null;
            listings.Add(new(ReadText(row.GetProperty("id"), "listing.id"), ReadText(account.GetProperty("name"), "listing.account.name"), new(value, ReadText(price.GetProperty("currency"), "listing.price.currency") switch { "exalted" => "Exalted Orb", "divine" => "Divine Orb", "chaos" => "Chaos Orb", var other => other }), text.ToString(), date, online, request.InstantBuyOnly, icon, item.TryGetProperty("stackSize",out var stackSize) && stackSize.ValueKind == JsonValueKind.Number && stackSize.TryGetInt32(out int stock) && stock > 0 ? stock : (int?)null, ItemSockets.Parse(item)));
        }
        var document = new ListingDocument(request.League, "POE trade · first 30 offers · selected filters checked locally", now, listings);
        return ComparableMarket.Parse(JsonSerializer.Serialize(document), now).Search(request, now);
    }
}
