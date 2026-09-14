using ExileLens.Core;
using System.Net;
using System.Text.Json;

static class LiveTradeChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var staff=ItemParser.Parse("Item Class: Staves\nRarity: Magic\nAzure Chiming Staff of the Skilled\nChiming Staff\n--------\nItem Level: 81\nGrants Skill: Level 20 Sigil of Power\n--------\n+62 to maximum Mana\n30% reduced Attribute Requirements")!;
        var staffRequest=new ComparableRequest(staff,"Test League","Auto",new[]{new PriceConstraint("Item Level: 81",81,null,Kind:"Property"),new PriceConstraint("Grants Skill: Level 20 Sigil of Power",20,null,Kind:"Property")},false,false,null);
        using var staffCatalog=JsonDocument.Parse("""{"result":[{"entries":[{"id":"skill.sigil_of_power","text":"Grants Skill: Level # Sigil of Power"}]}]}""");
        using var staffQuery=JsonDocument.Parse(LiveTradeClient.BuildQuery(staffRequest,staffCatalog.RootElement));
        var staffStat=staffQuery.RootElement.GetProperty("query").GetProperty("stats")[0].GetProperty("filters")[0];
        check(staffStat.GetProperty("id").GetString()=="skill.sigil_of_power" && staffStat.GetProperty("value").GetProperty("min").GetInt32()==20,"Granted skill level is filtered on the server before pagination");
        check(staffQuery.RootElement.GetProperty("query").GetProperty("filters").GetProperty("type_filters").GetProperty("filters").GetProperty("ilvl").GetProperty("min").GetInt32()==81,"Granted skill search retains selected item level");
        var unidentified = ItemParser.Parse("Item Class: Body Armours\nRarity: Unique\nPilgrim Vestments\n--------\nArmour: 25\nEnergy Shield: 16\n--------\nItem Level: 75\n--------\nUnidentified")!;
        var unidRequest = new ComparableRequest(unidentified,"Test League","Exalted Orb",Array.Empty<PriceConstraint>(),false,false,null);
        var unidQuery = JsonDocument.Parse(LiveTradeClient.BuildQuery(unidRequest,null)).RootElement.GetProperty("query");
        check(!unidQuery.TryGetProperty("name",out _) && unidQuery.GetProperty("type").GetString()=="Pilgrim Vestments", "Unidentified unique searches its base without a fabricated unique name");
        check(unidQuery.GetProperty("filters").GetProperty("misc_filters").GetProperty("filters").GetProperty("identified").GetProperty("option").GetString()=="false" && unidQuery.GetProperty("filters").GetProperty("type_filters").GetProperty("filters").GetProperty("rarity").GetProperty("option").GetString()=="unique", "Unidentified search retains rarity and identification state");
        var url = TradeSearch.BuildUrl(unidentified,"Test League");
        var browserQuery=JsonDocument.Parse(Uri.UnescapeDataString(url.Split("?q=")[1])).RootElement.GetProperty("query");
        check(!browserQuery.TryGetProperty("name",out _) && browserQuery.GetProperty("filters").GetProperty("misc_filters").GetProperty("filters").GetProperty("identified").GetProperty("option").GetString()=="false", "Browser unidentified search agrees with live query");
        var identified=ItemParser.Parse(unidentified.Details.Replace("Pilgrim Vestments\n", "Known Unique Name\nPilgrim Vestments\n").Replace("\nUnidentified", ""))!;
        var identifiedQuery=JsonDocument.Parse(LiveTradeClient.BuildQuery(unidRequest with { Item=identified },null)).RootElement.GetProperty("query");
        check(identifiedQuery.GetProperty("name").GetString()=="Known Unique Name", "Identified uniques retain exact-name searches");
        var unidMagic=ItemParser.Parse(unidentified.Details.Replace("Rarity: Unique","Rarity: Magic"))!;
        check(LiveTradeClient.BuildQuery(unidRequest with { Item=unidMagic },null).Contains("Pilgrim Vestments") && TradeSearch.BuildUrl(unidMagic,"Test League").Contains("Pilgrim"), "Unidentified magic base does not trigger affix-ambiguity rejection");
        check(DefaultItemFilters.Select(unidMagic).Count==0, "Unidentified items do not preselect visible base defences as roll filters");
        var unidNow=DateTimeOffset.UtcNow;
        var unidDoc=new ListingDocument("Test League","fixture",unidNow,new[] {
            new ImportedListing("unid","seller1",new ListingPrice(10,"Exalted Orb"),identified.Details+"\nUnidentified",unidNow,true,false),
            new ImportedListing("id","seller2",new ListingPrice(20,"Exalted Orb"),identified.Details,unidNow,true,false)
        });
        var unidResult=ComparableMarket.Parse(JsonSerializer.Serialize(unidDoc),unidNow).Search(unidRequest,unidNow);
        check(unidResult.Rows.Count==1 && unidResult.Rows[0].Listing.Id=="unid", "Unidentified listings accept source names but exclude identified items");
        check(SimilarItems.Estimate(unidentified,unidResult).Explanation.Contains("Hidden identity"), "Unidentified estimate explains hidden-information limitation");
        var magicAmulet=ItemParser.Parse(File.ReadAllText("tests/Fixtures/magic-crimson-amulet.txt"))!;
        check(magicAmulet.BaseType=="Crimson Amulet" && magicAmulet.Name=="Glimmering Crimson Amulet of the Universe", "Magic name preserves affixes while resolving the known base");
        var magicQuery=JsonDocument.Parse(LiveTradeClient.BuildQuery(unidRequest with { Item=magicAmulet },null)).RootElement.GetProperty("query");
        check(magicQuery.GetProperty("type").GetString()=="Crimson Amulet" && !magicQuery.TryGetProperty("name",out _), "Affixed magic item searches its base without a name filter");
        check(TradeSearch.BuildUrl(magicAmulet,"Test League").Contains("Crimson%20Amulet"), "Browser search uses resolved magic base");
        check(BaseTypes.ResolveMagicName("Crimson Amulet of the Universe")=="Crimson Amulet" && BaseTypes.ResolveMagicName("Glimmering Crimson Amulet")=="Crimson Amulet", "Suffix-only and prefix-only magic names resolve");
        check(BaseTypes.ResolveMagicName("Glimmering Unknown Amulet of the Universe")==null, "Unknown magic base remains unresolved rather than guessed");
        check(BaseTypes.ResolveMagicName("Crimson Amulet of Gold Ring")==null, "Conflicting base names remain ambiguous");
        var item = ItemParser.Parse("Rarity: Rare\nTest Ring\nGold Ring\n--------\nItem Level: 80\n+100 to maximum Life")!;
        var request = new ComparableRequest(item, "Test League", "Divine Orb", [new("+100 to maximum Life", 90, 120, 0, "Item text", 0)], false, true, 3);
        var catalog = JsonDocument.Parse("""{"result":[{"entries":[{"id":"explicit.life","text":"+# to maximum Life"},{"id":"implicit.life","text":"+# to maximum Life"}]}]}""").RootElement;
        var query = JsonDocument.Parse(LiveTradeClient.BuildQuery(request, catalog)).RootElement.GetProperty("query");
        check(query.GetProperty("status").GetProperty("option").GetString() == "securable", "Live instant buy uses securable status");
        var groups = query.GetProperty("filters");
        check(groups.GetProperty("trade_filters").GetProperty("filters").GetProperty("collapse").GetProperty("option").GetString()=="true", "Search collapses duplicate seller offers");
        check(groups.GetProperty("trade_filters").GetProperty("filters").GetProperty("price").GetProperty("option").GetString() == "divine" && groups.GetProperty("trade_filters").GetProperty("filters").GetProperty("indexed").GetProperty("option").GetString() == "3days", "Live currency and listing age are sent to trade");
        var stat = query.GetProperty("stats")[0].GetProperty("filters")[0];
        check(stat.GetProperty("id").GetString() == "explicit.life" && stat.GetProperty("value").GetProperty("min").GetDecimal() == 90 && stat.GetProperty("value").GetProperty("max").GetDecimal() == 120, "Live modifier ID keeps kind and both bounds");
        var levelRequest = request with { Filters = [new("Item Level: 80", 80, 84, 0, "Property")] };
        var levelQuery = JsonDocument.Parse(LiveTradeClient.BuildQuery(levelRequest, null)).RootElement;
        check(levelQuery.GetProperty("query").GetProperty("filters").GetProperty("type_filters").GetProperty("filters").GetProperty("ilvl").GetProperty("max").GetInt32() == 84, "Live item level uses trade2 type filters");
        bool rejected = false;
        try { LiveTradeClient.BuildQuery(request with { Filters = [new("Unknown modifier 10", 10, null)] }, catalog); }
        catch (ArgumentException) { rejected = true; }
        check(rejected, "Unmapped live filters cannot silently broaden a query");
        string Fixture(string id, string seller, int life, int price, string currency = "divine") => JsonSerializer.Serialize(new
        {
            id, listing = new { indexed = DateTimeOffset.UtcNow.AddHours(-1), account = new { name = seller, online = new { league = "Test League" } }, price = new { amount = price, currency } },
            item = new { frameType = 2, name = "Other Ring", baseType = "Gold Ring", ilvl = 82, identified = true, corrupted = false, explicitMods = new[] { $"+{life} to maximum Life" } }
        });
        string fetched = "{\"result\":[" + string.Join(",", new[] { Fixture("a", "A", 100, 10), Fixture("b", "B", 110, 20), Fixture("c", "C", 120, 30), Fixture("d", "D", 150, 2), Fixture("e", "E", 100, 1, "exalted") }) + "]}";
        var result = LiveTradeClient.ParseListings(JsonDocument.Parse(fetched).RootElement, request, DateTimeOffset.UtcNow);
        check(result.Rows.Count == 3 && result.Median == 20, "Live response excludes wrong bounds and currency before estimating");
        var skillResponse=System.Text.Json.Nodes.JsonNode.Parse(fetched)!;
        foreach(var row in skillResponse["result"]!.AsArray())
        {
            row!["item"]!["grantedSkills"]=System.Text.Json.Nodes.JsonNode.Parse("""[{"name":"Grants Skill","values":[["Level 18 Power Siphon",25]],"displayMode":0},{"name":"Grants Skill","values":[["[PinnacleOfPower|Pinnacle of Power]",25]],"displayMode":0}]""");
            row["item"]!["properties"]=System.Text.Json.Nodes.JsonNode.Parse("""[{"name":"Grants Skill","values":[["Level 18 Power Siphon",25]],"displayMode":0}]""");
        }
        using(var skillJson=JsonDocument.Parse(skillResponse.ToJsonString()))
        {
            var skills=ItemAnalysis.From(LiveTradeClient.ParseListings(skillJson.RootElement,request,DateTimeOffset.UtcNow).Rows.First().Item).Lines;
            check(skills.Count(l=>l.Text=="Grants Skill: Level 18 Power Siphon")==1 && skills.Any(l=>l.Text=="Grants Skill: Pinnacle of Power" && l.Kind=="Property"),"Trade granted skills preserve all names, clean links and avoid property duplicates");
        }
        var broadRequest = request with { ExactBase=false, Item=request.Item with { ItemClass="Rings" } };
        var broadResult = LiveTradeClient.ParseListings(JsonDocument.Parse(fetched).RootElement,broadRequest,DateTimeOffset.UtcNow);
        check(broadResult.Rows.Count==3 && broadResult.Rows.All(r=>r.Item.ItemClass=="Rings"), "Broad-category listings preserve class instead of all being discarded");
        var categories=JsonDocument.Parse("""{"result":[{"filters":[{"id":"category","option":{"options":[{"id":"fixture.quarterstaff","text":"Quarterstaff"}]}}]}]}""").RootElement;
        check(LiveTradeClient.ResolveCategory(categories,"Quarterstaves")=="fixture.quarterstaff", "Broad quarterstaff search resolves provider category");
        var broadQuery=JsonDocument.Parse(LiveTradeClient.BuildQuery(broadRequest,catalog,"fixture.ring")).RootElement.GetProperty("query");
        check(broadQuery.GetProperty("filters").GetProperty("type_filters").GetProperty("filters").GetProperty("category").GetProperty("option").GetString()=="fixture.ring", "Disabling exact base retains category constraint on server");
        var modern = System.Text.Json.Nodes.JsonNode.Parse(fetched)!;
        foreach (var row in modern["result"]!.AsArray())
        {
            var gear = row!["item"]!;
            gear["rarity"] = "Rare";
            gear["icon"] = "https://web.poecdn.com/image/Art/2DItems/Rings/GoldRing.png";
            gear.AsObject().Remove("frameType");
            var description = gear["explicitMods"]![0]!.GetValue<string>();
            gear["explicitMods"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
            {
                ["description"] = description,
                ["flags"] = new System.Text.Json.Nodes.JsonObject { ["desecrated"] = true }
            });
            gear["implicitMods"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["description"] = "+10 to maximum Mana\n+12 to maximum Life" });
        }
        var flagResult = LiveTradeClient.ParseListings(JsonDocument.Parse(modern.ToJsonString()).RootElement,request,DateTimeOffset.UtcNow);
        check(flagResult.Rows.All(r=>r.Analysis.Desecrated), "object modifier flags reach item presentation");
        var modernJson = JsonDocument.Parse(modern.ToJsonString()).RootElement;
        var modernResult = LiveTradeClient.ParseListings(modernJson, request, DateTimeOffset.UtcNow);
        check(modernResult.Rows.Count == 3 && modernResult.Median == 20, "Documented ItemMod descriptions preserve price bounds with current rarity field");
        check(modernResult.Rows.All(x => x.IconUrl == "https://web.poecdn.com/image/Art/2DItems/Rings/GoldRing.png"), "Trade artwork survives parsing and comparable filtering");
        check(ItemArtwork.SafeUrl("file:///C:/private.png") == null && ItemArtwork.SafeUrl("https://example.com/image.png") == null && ItemArtwork.SafeUrl("https://web.poecdn.com@localhost/image.png") == null, "Imported artwork cannot reference local files or unrelated servers");
        check(modernResult.Rows.All(x => x.Analysis.Lines.Count(l => l.Kind == "Implicit") == 2), "Multiline object modifiers preserve implicit kind on every line");
        using var modernHandler = new TradeHandler(catalog.GetRawText(), modern.ToJsonString());
        var modernLive = await new LiveTradeClient(new HttpClient(modernHandler)).SearchAsync(request, CancellationToken.None);
        check(modernLive.Median == 20 && modernLive.Rows.Count == 3, "Search and fetch pipeline handles object explicitMods without weakening selected filters");
        using var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        throttled.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(90));
        throttled.Headers.TryAddWithoutValidation("X-Rate-Limit-Ip", "5:10:60,15:60:300");
        throttled.Headers.TryAddWithoutValidation("X-Rate-Limit-Ip-State", "5:10:180,8:60:0");
        var now = DateTimeOffset.UtcNow;
        check(LiveTradeClient.Cooldown(throttled, now, now) == now.AddSeconds(180), "Live cooldown respects the longer server ban over Retry-After");
        using var budget=new HttpResponseMessage(HttpStatusCode.OK);
        budget.Headers.TryAddWithoutValidation("X-Rate-Limit-Ip","15:4:60,30:60:300");
        budget.Headers.TryAddWithoutValidation("X-Rate-Limit-Ip-State","1:4:0,3:60:0");
        check(LiveTradeClient.NextRequest(budget,now,now.AddSeconds(4))==now,"Available server budget removes artificial page delay");
        budget.Headers.Remove("X-Rate-Limit-Ip-State");
        budget.Headers.TryAddWithoutValidation("X-Rate-Limit-Ip-State","15:4:0,3:60:0");
        check(LiveTradeClient.NextRequest(budget,now,now)==now.AddSeconds(4),"Exhausted fetch budget still waits for the server window");
        using var noBudget=new HttpResponseMessage(HttpStatusCode.OK);
        check(LiveTradeClient.NextRequest(noBudget,now,now.AddSeconds(4))==now.AddSeconds(4),"Missing server headers retain fallback spacing");
        check(LiveTradeClient.NextRequest(throttled,now,now.AddSeconds(4))==now.AddSeconds(180),"Adaptive pacing preserves server bans and Retry-After");
        using var handler = new TradeHandler(catalog.GetRawText(), fetched);
        var client = new LiveTradeClient(new HttpClient(handler));
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        var stages = new List<string>();
        var live = await client.SearchAsync(request, CancellationToken.None, stages.Add);
        check(handler.Paths.Count == 3 && handler.Paths[1].Contains("/search/poe2/Test%20League") && handler.Paths[2].Contains("/fetch/a,b,c?query=test"), "Live adapter executes stat lookup then search then listing fetch");
        check(live.Rows.Count == 3 && live.Median == 20 && handler.SearchBody!.Contains("explicit.life"), "Full live adapter pipeline returns filtered prices without imported data");
        check(elapsed.Elapsed < TimeSpan.FromSeconds(3), "Independent endpoint requests do not add artificial search-to-fetch delays");
        check(stages.Contains("Loading trade filters…") && stages.Any(s=>s.StartsWith("Loading listing page 1 of")), "Search progress identifies the current request stage");
        using var objects = new TradeHandler(catalog.GetRawText(), fetched) { SearchResponse = """{"id":"test","result":[{"id":"a"},"b",{"id":"c"}],"total":3}""" };
        var objectResult = await new LiveTradeClient(new HttpClient(objects)).SearchAsync(request, CancellationToken.None);
        check(objectResult.Median == 20 && objects.Paths[2].Contains("/fetch/a,b,c?query=test"), "Object and string search result IDs both fetch and price correctly");
        using var embedded = new TradeHandler(catalog.GetRawText(), fetched) { SearchResponse = fetched.Replace("{\"result\":", "{\"id\":\"test\",\"result\":") };
        var embeddedResult = await new LiveTradeClient(new HttpClient(embedded)).SearchAsync(request, CancellationToken.None);
        check(embeddedResult.Median == 20 && embedded.Paths.Count == 2, "Complete search listings are priced without a redundant fetch");
        using var malformed = new TradeHandler(catalog.GetRawText(), fetched) { SearchResponse = """{"id":"test","result":[{"unexpected":"shape"}]}""" };
        bool fieldError = false;
        try { await new LiveTradeClient(new HttpClient(malformed)).SearchAsync(request, CancellationToken.None); }
        catch (JsonException ex) { fieldError = ex.Message.Contains("search.result[0].id") && ex.Message.Contains("Object"); }
        check(fieldError, "Unknown search objects identify the failing field rather than throwing a generic string error");
        var cached = await client.SearchAsync(request,CancellationToken.None);
        check(cached.Source.Contains("cached") && handler.Paths.Count==3 && cached.SearchUrl!=null && cached.FetchedCount>0,"Identical search uses cache and retains exact query and fetch metadata");
        string tenOffers = "{\"result\":[" + string.Join(",",Enumerable.Range(0,10).Select(i=>Fixture("page-"+i,"seller-"+i,100,10))) + "]}";
        using var paged = new TradeHandler(catalog.GetRawText(),tenOffers) { SearchResponse=JsonSerializer.Serialize(new { id="pages",total=30,result=Enumerable.Range(0,30).Select(i=>"id-"+i).ToArray() }) };
        var pagedResult=await new LiveTradeClient(new HttpClient(paged)).SearchAsync(request,CancellationToken.None);
        check(paged.Paths.Count==3 && pagedResult.Rows.Count==10 && pagedResult.TotalMatches==30,"Enough matching offers stop extra fetch batches without losing total count");
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));
        bool cancelledWait = false;
        try { await client.SearchAsync(request with { MaximumAgeDays=1 }, cancelled.Token); }
        catch (OperationCanceledException) { cancelledWait = true; }
        check(cancelledWait && handler.Paths.Count == 3, "Repeated search cooldown is cancellable and sends no premature request");
        using var denied = new HttpClient(new TradeHandler(catalog.GetRawText(), fetched, true));
        bool authMessage = false;
        try { await new LiveTradeClient(denied).SearchAsync(request with { Filters = [] }, CancellationToken.None); }
        catch (HttpRequestException ex) { authMessage = ex.Message.Contains("sign-in"); }
        check(authMessage, "Trade verification failures give a specific actionable status");
    }
    private sealed class TradeHandler(string catalog, string fetched, bool deny = false) : HttpMessageHandler
    {
        public List<string> Paths = [];
        public string? SearchBody;
        public string SearchResponse = """{"id":"test","result":["a","b","c"],"total":3}""";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken token)
        {
            string path = message.RequestUri!.AbsoluteUri;
            Paths.Add(path);
            if (message.Content != null) SearchBody = await message.Content.ReadAsStringAsync(token);
            return new HttpResponseMessage(deny ? HttpStatusCode.Forbidden : HttpStatusCode.OK)
            {
                Content = new StringContent(path.Contains("/data/stats") ? catalog : path.Contains("/search/") ? SearchResponse : fetched)
            };
        }
    }
}
