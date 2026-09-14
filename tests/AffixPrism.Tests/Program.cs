using AffixPrism.Core;
using System.Text.Json;

int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
UpdateChecks.Run(Check);
var charmCard = ItemParser.Parse("Item Class: Charms\nRarity: Unique\nBeira's Anguish\nDousing Charm\n--------\nLasts 3 Seconds\nConsumes 30 of 40 Charges on use\nCurrently has 40 Charges\nGrants Immunity to Ignite\n--------\nRequires: Level 32\n--------\nUsed when you become Ignited\n--------\nThey found a crying child at a frozen pyre.\n--------\nUsed automatically when condition is met.\nCan only hold charges while in belt.\nRefill at Wells or by killing monsters.")!;
var charmLines = ItemAnalysis.From(charmCard).Lines;
Check(charmLines.Where(l => l.Text.StartsWith("Lasts ") || l.Text.StartsWith("Consumes ") || l.Text.StartsWith("Currently has ")).All(l => l.Kind == "Property"), "Charm duration and charges are properties");
Check(charmLines.Single(l => l.Text.StartsWith("Grants Immunity")).Kind == "Implicit", "Charm immunity uses modifier styling");
Check(charmLines.Single(l => l.Text.StartsWith("Used automatically")).Kind == "Instructions" && charmLines.Single(l => l.Text.StartsWith("Refill at")).Kind == "Instructions", "Multiline charm usage is separate from lore");

string Line(string id = "ExpeditionLogBook_Atoll") => $"2025/12/31 21:36:10 473486140 2caa22d9 [DEBUG Client 31436] Generating level 80 area \"{id}\" with seed 2676229948";
Check(AreaParser.Parse(Line()) is { IsLogbook: true, Level: 80, DisplayName: "Atoll Logbook" }, "Observed Logbook entry");
Check(AreaParser.Parse(Line("ExpeditionLeagueBoss")) is { IsExpedition: true, IsLogbook: false }, "Expedition boss");
Check(AreaParser.Parse(Line("ExpeditionSubArea_Kalguur_Act1")) is { IsExpedition: false }, "Campaign expedition does not trigger Logbook guide");
Check(AreaParser.Parse(Line("HideoutBlankUrban")) is { IsExpedition: false }, "Departure clears Expedition context");
Check(AreaParser.Parse("2026/09/08 [INFO Client 1] #Player: " + Line()) is null, "Chat cannot spoof area events");
Check(AreaParser.Parse("AtlasExpeditionNotable1 is missing") is null, "Asset warnings ignored");
Check(ItemParser.Parse("Item Class: Rings\r\nRarity: Rare\r\nDoom Circle\r\nGold Ring\r\n--------\r\nItem Level: 80") is { Name: "Doom Circle", BaseType: "Gold Ring", ItemClass: "Rings" }, "Rare clipboard item");
Check(ItemParser.Parse("Rarity: Currency\nDivine Orb\n--------\nStack Size: 1/10") is { Name: "Divine Orb" }, "Currency clipboard item");
Check(ItemParser.Parse("ordinary clipboard text") is null, "Non-item clipboard rejected");
var path = Path.GetTempFileName();
try
{
    File.WriteAllText(path, Line() + "\n");
    var tail = new LogTail(path);
    Check(tail.Poll().Count == 0, "Historical area is not replayed at startup");
    File.AppendAllText(path, Line()[..50]);
    Check(tail.Poll().Count == 0, "Partial line waits for completion");
    File.AppendAllText(path, Line()[50..] + "\n" + Line("HideoutBlankUrban") + "\n");
    var entries = tail.Poll();
    Check(entries.Count == 2 && entries[0].IsLogbook && !entries[1].IsExpedition, "Appended transitions preserved in order");
    Check(tail.Poll().Count == 0, "Unchanged log does not repeat events");
    File.WriteAllText(path, Line("ExpeditionLeagueBoss") + "\n");
    Check(tail.Poll().Single().IsExpedition, "Truncated log resumes reading");
}
finally { File.Delete(path); }
Check(CheckHotkey.Parse("alt+e") == CheckHotkey.Default, "Default hotkey is Alt+E");
Check(CheckHotkey.Parse("Ctrl+Shift+F6")?.Label == "Ctrl+Shift+F6", "Custom shortcut round trip");
Check(CheckHotkey.Parse("E") is null && CheckHotkey.Parse("Ctrl+C") is null && CheckHotkey.Parse("Ctrl+Alt+O") is null, "Unsafe or reserved shortcuts rejected");
var rare = ItemParser.Parse("Item Class: Rings\nRarity: Rare\nDoom Circle\nGold Ring\n--------\nItem Level: 80")!;
var url = TradeSearch.BuildUrl(rare, "Test league / one");
using (var json = JsonDocument.Parse(Uri.UnescapeDataString(url[(url.IndexOf("?q=") + 3)..])))
{
    var query = json.RootElement.GetProperty("query");
    Check(query.GetProperty("type").GetString() == "Gold Ring" && !query.TryGetProperty("name", out _), "Rare search uses base, not random item name");
    Check(query.GetProperty("filters").GetProperty("type_filters").GetProperty("filters").GetProperty("rarity").GetProperty("option").GetString() == "rare", "Rare search constrains rarity");
}
Check(url.Contains("Test%20league%20%2F%20one"), "League safely encoded in search URL");
var unique = new CopiedItem("Test \"Unique\"", "Gold Ring", "Unique", "Rings", "");
using (var json = JsonDocument.Parse(Uri.UnescapeDataString(TradeSearch.BuildUrl(unique, "Standard").Split("?q=")[1])))
    Check(json.RootElement.GetProperty("query").GetProperty("name").GetString() == unique.Name, "Unique identity and quotes survive encoding");
try { TradeSearch.BuildUrl(rare, " "); Check(false, "Missing league rejected"); } catch (ArgumentException) { Check(true, "Missing league rejected"); }
var fake = new CaptureFake { Text = rare.Details };
var result = await new ItemCapture(fake).CaptureAsync(CancellationToken.None, 40, 1);
Check(result.Item?.Name == "Doom Circle" && fake.CopyCalls == 1, "One shortcut copies once and parses a fresh item");
fake = new CaptureFake { Text = rare.Details, UpdateClipboard = false };
result = await new ItemCapture(fake).CaptureAsync(CancellationToken.None, 15, 1);
Check(result.Item == null && fake.CopyCalls == 1, "Stale clipboard times out without repeated copy input");
fake = new CaptureFake { Game = 0 };
result = await new ItemCapture(fake).CaptureAsync(CancellationToken.None, 15, 1);
Check(result.Item == null && fake.CopyCalls == 0, "No input sent outside POE2");
fake = new CaptureFake { Released = false };
result = await new ItemCapture(fake).CaptureAsync(CancellationToken.None, 15, 1);
Check(result.Item == null && fake.CopyCalls == 0, "Held modifiers never leak into Ctrl+C");
fake = new CaptureFake { LoseFocusOnCopy = true };
result = await new ItemCapture(fake).CaptureAsync(CancellationToken.None, 15, 1);
Check(result.Item == null, "Focus loss cancels clipboard capture");
fake = new CaptureFake { Text = "Not an item" };
result = await new ItemCapture(fake).CaptureAsync(CancellationToken.None, 15, 1);
Check(result.Item == null && result.Error!.Contains("supported item"), "Non-item fresh text cannot launch an item search");
fake = new CaptureFake { Text = rare.Details, BusyReads = 2 };
result = await new ItemCapture(fake).CaptureAsync(CancellationToken.None, 80, 1);
Check(result.Item != null && fake.CopyCalls == 1, "Busy clipboard is retried without resending copy");
Check(result.Stage == "complete", "Successful capture reports completed stage");
fake = new CaptureFake { UpdateClipboard = false };
result = await new ItemCapture(fake).CaptureAsync(CancellationToken.None, 15, 1);
Check(result.Stage == "clipboard-unchanged", "Copy timeout identifies unchanged clipboard");
fake = new CaptureFake { BusyReads = 1000 };
result = await new ItemCapture(fake).CaptureAsync(CancellationToken.None, 15, 1);
Check(result.Stage == "clipboard-busy", "Changed but unreadable clipboard has distinct diagnostic");
var keys = new KeyFake();
Check(await CopyChord.SendAsync(keys, CancellationToken.None), "Frame-spaced copy chord succeeds");
Check(string.Join(",", keys.Events.Select(x => x.Key)) == "17:True,67:True,67:False,17:False", "Chord contains exactly one copy press and releases both keys");
Check(keys.Events[2].Time - keys.Events[1].Time >= 70, "Copy key held across multiple frames");
using (var cancellation = new CancellationTokenSource())
{
    keys = new KeyFake { AfterKey = key => { if (key == 67) cancellation.Cancel(); } };
    try { await CopyChord.SendAsync(keys, cancellation.Token); } catch (OperationCanceledException) { }
    Check(keys.Events.TakeLast(2).Select(x => x.Key).SequenceEqual(new[] { "67:False", "17:False" }), "Cancellation releases C and Ctrl");
}
keys = new KeyFake { FailCopyDown = true };
Check(!await CopyChord.SendAsync(keys, CancellationToken.None) && keys.Events[^1].Key == "17:False", "Failed C press still releases Ctrl");
keys = new KeyFake { LoseFocusAfterControl = true };
Check(!await CopyChord.SendAsync(keys, CancellationToken.None) && keys.Events.Count == 2 && keys.Events[^1].Key == "17:False", "Focus loss during chord releases Ctrl without C input");
var leagues = Leagues.Parse("[{\"id\":\"League ID\",\"name\":\"Friendly league\"},{\"id\":\"League ID\"},{\"id\":\"\"}]");
Check(leagues.Count == 1 && leagues[0].Name == "Friendly league", "League IDs and display names parsed with duplicate rejection");
Check(Leagues.WithSaved(leagues, "My saved league").Last().Id == "My saved league", "League refresh preserves saved league");
Check(Leagues.Bundled.Any(x => x.Id == "Standard") && Leagues.Bundled.Any(x => x.Id == "Hardcore"), "Offline league list includes permanent trade leagues");
try { Leagues.Parse("{}"); Check(false, "Invalid league response rejected"); } catch (JsonException) { Check(true, "Invalid league response rejected"); }

var analysis = ItemAnalysis.From(ItemParser.Parse("Rarity: Rare\nDoom Circle\nGold Ring\n--------\nQuality: +20%\nItem Level: 80\nCorrupted")!);
Check(analysis.ItemLevel == 80 && analysis.Quality == 20 && analysis.Corrupted, "Item properties parsed");
var filteredUrl = TradeSearch.BuildUrl(rare, "Standard", new TradeFilters("Gold Ring", true, 80, 10, false));
var filteredJson = Uri.UnescapeDataString(filteredUrl.Split("?q=")[1]);
using (var filtered = JsonDocument.Parse(filteredJson))
    Check(filtered.RootElement.GetProperty("query").GetProperty("filters").GetProperty("type_filters").GetProperty("filters").GetProperty("ilvl").GetProperty("min").GetInt32() == 80, "Trade minimum level serialized");
const string marketJson = """{"core":{"primary":"exalted","items":[{"id":"exalted","name":"Exalted Orb"},{"id":"divine","name":"Divine Orb"}]},"lines":[{"id":"divine","primaryValue":120},null,{"name":"Broken","primaryValue":"bad"}]}""";
var marketRows = Economy.Parse(marketJson, true);
var displayNow=DateTimeOffset.UtcNow;
var displayRates=new EconomySnapshot(new[]{new EconomyRow("Exalted Orb","","",.004m,"Divine Orb",null,null)},displayNow,false,false);
var inexpensive=new EconomyRow("Example","","",.02m,"Divine Orb",null,null);
Check(MarketPriceDisplay.Format(inexpensive,displayRates,displayNow)=="5 Exalted Orb","Sub-divine prices use fresh exalted rates");
Check(MarketPriceDisplay.Format(inexpensive with {Value=2},displayRates,displayNow)=="2 Divine Orb","Prices at least one divine retain divine display");
Check(MarketPriceDisplay.Format(inexpensive,displayRates with {Stale=true},displayNow)=="0.02 Divine Orb","Stale conversion rates retain original currency");
Check(MarketPriceDisplay.Format(inexpensive,displayRates,displayNow.AddHours(2))=="0.02 Divine Orb","Expired conversion rates are not used");
Check((inexpensive with {Value=.000001m}).PriceLabel=="0.000001 Divine Orb","Small positive prices do not round to zero");
var hoverRates=displayRates with {Rows=displayRates.Rows.Concat(new[]{new EconomyRow("Chaos Orb","","",.1m,"Divine Orb",null,null)}).ToArray()};
var hoverCurrencies=MarketHoverDetails.Currencies(inexpensive,hoverRates,displayNow);
Check(hoverCurrencies.Contains("5 Exalted Orb") && hoverCurrencies.Contains("0.2 Chaos Orb") && hoverCurrencies.Contains("0.02 Divine Orb"),"Price hover shows fresh popular-currency equivalents");
Check(MarketHoverDetails.Currencies(inexpensive,hoverRates with {Stale=true},displayNow).Contains("Fresh exchange rates unavailable"),"Price hover does not present stale conversions");
var hoverHistory=MarketHoverDetails.History(inexpensive with {History=new decimal?[]{null,0,12,-5},ChangePercent=-5},displayNow);
Check(hoverHistory.Contains("Sample 1: unavailable") && hoverHistory.Contains("Sample range: -5% to +12%") && hoverHistory.Contains("not historical sale prices"),"History hover explains missing points and relative changes");
var referenceRows = Economy.Parse("""{"core":{"primary":"exalted","items":[{"id":"exalted","name":"Exalted Orb"}]},"items":[{"id":"demo","name":"Demo Rune","image":"/gen/image/demo.png"}],"lines":[{"id":"demo","primaryValue":5,"sparkline":{"totalChange":-8,"data":[null,0,2,-8]}}]}""", true);
Check(referenceRows[0].IconUrl=="https://web.poecdn.com/gen/image/demo.png", "Market artwork resolves official relative image paths");
Check(referenceRows[0].ChangePercent==-8 && referenceRows[0].History.SequenceEqual(new decimal?[]{null,0,2,-8}), "Market trend preserves provider values and missing history points");
Check(marketRows.Count == 1 && marketRows[0].Name == "Divine Orb" && marketRows[0].Currency == "Exalted Orb", "Exchange metadata resolves identity and currency; malformed rows skipped");
Check(Economy.Matching(marketRows, ItemParser.Parse("Rarity: Currency\nDivine Orb\n--------\nStack Size: 1/10")!).Count == 1, "Market identity matches copied item");
Check(Economy.Category(rare) == null, "Rare items never receive generic automatic valuation");
string cacheDir = Path.Combine(Path.GetTempPath(), "AffixPrism-test-" + Guid.NewGuid());
try
{
    var handler = new MarketHandler(marketJson);
    var client = new EconomyClient(new HttpClient(handler), cacheDir);
    var first = await client.CategoryAsync(new("Currency", true), "Standard", CancellationToken.None);
    var second = await client.CategoryAsync(new("Currency", true), "Standard", CancellationToken.None);
    Check(!first.Cached && second.Cached && handler.Calls == 1, "Fresh economy cache avoids repeat requests");
    handler.Fail = true;
    try { await client.CategoryAsync(new("Ritual", true), "Standard", CancellationToken.None); } catch (HttpRequestException) { }
    int calls = handler.Calls;
    try { await client.CategoryAsync(new("Ritual", true), "Standard", CancellationToken.None); } catch (HttpRequestException) { }
    Check(handler.Calls == calls, "Rate limiting prevents immediate retry");
    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    try { await client.CategoryAsync(new("Currency", true), "Standard", cancelled.Token); Check(false, "Cancelled market request"); }
    catch (OperationCanceledException) { Check(true, "Cancelled market request"); }
}
finally { if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true); }

var advancedItem = ItemParser.Parse("Item Class: Quarterstaves\nRarity: Rare\nMaelstrom Song\nLunar Quarterstaff\n--------\nItem Level: 82\n{ Prefix Modifier \"Devastating\" (Tier: 1) - Damage,\nLightning }\n123(120-139)% increased Elemental Damage with Attacks\n{ Prefix Modifier \"Emperor's\" (Tier: 3) }\n69(65-74)% increased Physical Damage\n+172(150-174) to Accuracy Rating\n{ Crafted Suffix Modifier \"of Infamy\" (Tier: 2) }\n24(23-25)% increased Attack Speed")!;
var parsedAdvanced = ItemAnalysis.From(advancedItem);
Check(parsedAdvanced.Lines.Count == 5 && parsedAdvanced.Lines.All(x => !x.Text.Contains('{') && !x.Text.Contains("Tier:")), "Advanced metadata never becomes item display rows");
Check(parsedAdvanced.Lines[1].Text == "123% increased Elemental Damage with Attacks" && parsedAdvanced.Lines[1].Tier == 1, "Roll bounds hidden and tier retained");
Check(parsedAdvanced.Lines[2].Tier == 3 && parsedAdvanced.Lines[3].Tier == 3, "Hybrid modifier lines share their metadata");
Check(parsedAdvanced.Lines[4].RollLabel == "50%", "Roll position comes from actual copied bounds");
var dualRoll = ItemAnalysis.From(ItemParser.Parse("Rarity: Rare\nName\nBase\n--------\nAdds 42(39-53) to 61(59-80) Fire Damage")!).Lines.Single();
Check(dualRoll.Text == "Adds 42 to 61 Fire Damage" && dualRoll.Rolls!.Count == 2 && dualRoll.RollLabel == "", "Multiple rolls preserved without misleading single percentage");
var decimalRoll = ItemAnalysis.From(ItemParser.Parse("Rarity: Rare\nName\nBase\n--------\n+1.17(1.01-1.5)% to Critical Hit Chance")!).Lines.Single();
Check(decimalRoll.Text.StartsWith("+1.17%") && decimalRoll.Rolls![0].Value == 1.17m, "Signed decimal rolls preserve precision");
var boundedUrl = Uri.UnescapeDataString(TradeSearch.BuildUrl(rare, "Standard", new TradeFilters("Gold Ring", MaxItemLevel: 85)).Split("?q=")[1]);
using (var bounded = JsonDocument.Parse(boundedUrl))
    Check(bounded.RootElement.GetProperty("query").GetProperty("filters").GetProperty("type_filters").GetProperty("filters").GetProperty("ilvl").GetProperty("max").GetInt32() == 85, "Maximum-only filter serialized");
try { TradeSearch.BuildUrl(rare, "Standard", new TradeFilters("Gold Ring", MinItemLevel: 90, MaxItemLevel: 80)); Check(false, "Inverted filter range rejected"); }
catch (ArgumentException) { Check(true, "Inverted filter range rejected"); }

var localFilter = new EvaluationFilter(new ItemLine("+120 to maximum Life", "Item text"));
localFilter.Preset(true);
Check(localFilter.Minimum == "108", "Broad local preset uses copied value minus ten percent");
localFilter.Preset(false);
Check(localFilter.Minimum == "120" && localFilter.Maximum == "", "Exact local preset resets bounds");
localFilter.Enabled = true; localFilter.Minimum = "10"; localFilter.Maximum = "2";
Check(localFilter.Validate() != null, "Local filter rejects reversed bounds");
localFilter.Minimum = "invalid";
Check(localFilter.Validate() != null, "Local filter rejects invalid number");
localFilter.Enabled = false;
Check(localFilter.Validate() == null, "Excluded filters do not block local draft");

var marketNow = DateTimeOffset.UtcNow;
string ComparableText(int life) => $"Item Class: Rings\nRarity: Rare\nSample Name\nGold Ring\n--------\nItem Level: 80\n+{life} to maximum Life";
var offers = new[] {
    new ImportedListing("a", "seller1", new(10, "Exalted Orb"), ComparableText(100), marketNow.AddHours(-2), true, true),
    new ImportedListing("b", "seller2", new(20, "Exalted Orb"), ComparableText(110), marketNow.AddHours(-2), true, true),
    new ImportedListing("c", "seller3", new(30, "Exalted Orb"), ComparableText(120), marketNow.AddHours(-2), true, true),
    new ImportedListing("d", "seller1", new(1000, "Exalted Orb"), ComparableText(100), marketNow.AddHours(-2), true, true),
    new ImportedListing("e", "seller4", new(1, "Divine Orb"), ComparableText(100), marketNow.AddHours(-2), true, true)
};
var dataset = new ListingDocument("Standard", "TEST FIXTURE", marketNow, offers);
var comparableMarket = ComparableMarket.Parse(JsonSerializer.Serialize(dataset), marketNow);
var comparableRequest = new ComparableRequest(ItemParser.Parse(ComparableText(100))!, "Standard", "Exalted Orb", Array.Empty<PriceConstraint>(), true, true, 7);
var estimate = comparableMarket.Search(comparableRequest, marketNow);
Check(estimate.Rows.Count == 4 && estimate.SellerCount == 3 && estimate.Median == 20, "Estimator separates currencies and counts each seller once");
Check(estimate.LowerQuartile == 15 && estimate.UpperQuartile == 25, "Estimator reports interpolated middle fifty percent");
var strictEstimate = comparableMarket.Search(comparableRequest with { Filters = new[] { new PriceConstraint("+100 to maximum Life", 110, null) } }, marketNow);
Check(strictEstimate.Rows.Count == 2 && strictEstimate.Median == null, "Selected modifier bounds apply and sparse data has no estimate");
Check(comparableMarket.Search(comparableRequest with { Item = ItemParser.Parse(ComparableText(100).Replace("Gold Ring", "Iron Ring"))! }, marketNow).Rows.Count == 0, "Different bases are never compared");
Check(comparableMarket.Search(comparableRequest with { MaximumAgeDays = 0 }, marketNow).Rows.Count == 0, "Listing age filter applied");
try { comparableMarket.Search(comparableRequest with { League = "Other" }, marketNow); Check(false, "Cross-league comparison rejected"); } catch (ArgumentException) { Check(true, "Cross-league comparison rejected"); }
try { ComparableMarket.Parse(JsonSerializer.Serialize(dataset with { CapturedAt = marketNow.AddDays(-31) }), marketNow); Check(false, "Expired dataset rejected"); } catch (JsonException) { Check(true, "Expired dataset rejected"); }
try { ComparableMarket.Parse(JsonSerializer.Serialize(dataset with { Listings = new[] { offers[0], offers[0] } }), marketNow); Check(false, "Duplicate IDs rejected"); } catch (JsonException) { Check(true, "Duplicate IDs rejected"); }

string repositoryPath = Path.Combine(Path.GetTempPath(), "AffixPrism-repository-" + Guid.NewGuid());
try
{
    var repository = new MarketRepository(repositoryPath);
    Check(await repository.SearchAsync(comparableRequest, CancellationToken.None) == null, "Missing dataset returns no result");
    await repository.ImportAsync(JsonSerializer.Serialize(dataset), CancellationToken.None);
    Check((await repository.SearchAsync(comparableRequest, CancellationToken.None))?.Median == 20, "In-process repository returns estimate");
    var reopened = new MarketRepository(repositoryPath);
    Check((await reopened.SearchAsync(comparableRequest, CancellationToken.None))?.Median == 20, "Imported dataset survives repository restart");
    await repository.ImportAsync(JsonSerializer.Serialize(dataset with { Listings = offers.Take(2).ToArray() }), CancellationToken.None);
    Check((await repository.SearchAsync(comparableRequest, CancellationToken.None))?.SellerCount == 2, "Dataset replacement invalidates cached estimate");
}
finally { if (Directory.Exists(repositoryPath)) Directory.Delete(repositoryPath, true); }
var changes = ItemComparison.Describe(ItemParser.Parse(ComparableText(100))!, ItemParser.Parse(ComparableText(120))!);
Check(changes.Contains("[+20]"), "Comparison displays numerical modifier change");

Check(new EvaluationFilter(new ItemLine("Physical Damage: 85-174", "Property"), 1).CopiedValue == 174, "Damage range separator does not become a negative sign");
var damageOffers = new[] {
    new ImportedListing("range1", "rangeSeller", new(10, "Exalted Orb"), ComparableText(100).Replace("+100 to maximum Life", "Adds 10 to 20 Fire Damage"), marketNow, true, true),
    new ImportedListing("range2", "rangeSeller2", new(20, "Exalted Orb"), ComparableText(100).Replace("+100 to maximum Life", "Adds 10 to 50 Fire Damage"), marketNow, true, true)
};
var rangeMarket = ComparableMarket.Parse(JsonSerializer.Serialize(dataset with { Listings = damageOffers }), marketNow);
var rangeResult = rangeMarket.Search(comparableRequest with { Filters = new[] { new PriceConstraint("Adds 10 to 20 Fire Damage", 40, null, 1) } }, marketNow);
Check(rangeResult.Rows.Count == 1 && rangeResult.Rows[0].Account == "rangeSeller2", "Secondary modifier value participates in matching");

var duplicateText = ComparableText(100) + "\n+120 to maximum Life";
var oneVsTwo = new[] {
    offers[0] with { Id = "one", ItemText = ComparableText(150) },
    offers[1] with { Id = "two", ItemText = duplicateText }
};
var duplicateMarket = ComparableMarket.Parse(JsonSerializer.Serialize(dataset with { Listings = oneVsTwo }), marketNow);
var bothFilters = new[] { new PriceConstraint("+100 to maximum Life", 100, null, 0, "Item text", 1), new PriceConstraint("+120 to maximum Life", 120, null, 0, "Item text", 2) };
Check(duplicateMarket.Search(comparableRequest with { Filters = bothFilters }, marketNow).Rows.Single().Listing.Id == "two", "Repeated modifiers require distinct matching listing lines");
var splitValues = offers[0] with { Id = "split", ItemText = ComparableText(100) + "\nAdds 10 to 100 Fire Damage\nAdds 100 to 10 Fire Damage" };
var splitMarket = ComparableMarket.Parse(JsonSerializer.Serialize(dataset with { Listings = new[] { splitValues } }), marketNow);
Check(splitMarket.Search(comparableRequest with { Filters = new[] { new PriceConstraint("Adds 100 to 100 Fire Damage", 50, null, 0), new PriceConstraint("Adds 100 to 100 Fire Damage", 50, null, 1) } }, marketNow).Rows.Count == 0, "Multiple values must match on the same modifier line");
var corruptMarket = ComparableMarket.Parse(JsonSerializer.Serialize(dataset with { Listings = new[] { offers[0] with { ItemText = ComparableText(100) + "\nCorrupted" } } }), marketNow);
Check(corruptMarket.Search(comparableRequest, marketNow).Rows.Count == 0, "Corrupted and uncorrupted items never mix");
var kindMarket = ComparableMarket.Parse(JsonSerializer.Serialize(dataset with { Listings = new[] { offers[0] with { ItemText = ComparableText(100).Replace("+100 to maximum Life", "+100 to maximum Life (implicit)") } } }), marketNow);
Check(kindMarket.Search(comparableRequest with { Filters = new[] { new PriceConstraint("+100 to maximum Life", 100, null, 0, "Item text") } }, marketNow).Rows.Count == 0, "Implicit modifier cannot satisfy explicit filter");
Check(comparableMarket.Search(comparableRequest with { Currency = "Divine Orb" }, marketNow).Rows.Single().Listing.Id == "e", "Selected currency restricts actual listing results");
Check(ItemAnalysis.CleanTradeText("[Evasion|Evasion Rating]: 322; [Fire] damage") == "Evasion Rating: 322; Fire damage", "Trade markup preserves display labels");
var comparisonBase = ItemParser.Parse("Rarity: Rare\nMine\nGrand Bracers\n--------\nEvasion Rating: 186\n+12% to Fire Resistance")!;
var comparisonSeller = ItemParser.Parse("Rarity: Rare\nTheirs\nGrand Bracers\n--------\n[Evasion|Evasion Rating]: 322\n+30% to [Resistances|Cold Resistance]")!;
var comparedRows = ItemComparison.Rows(comparisonBase, comparisonSeller);
Check(comparedRows.Any(x => x.Yours == "Evasion Rating: 186" && x.Seller == "Evasion Rating: 322" && x.Change == "+136"), "Equivalent trade and clipboard stats align with a numeric delta");
Check(comparedRows.Count(x => x.Change is "Yours only" or "Seller only") == 2, "Different elemental resistances remain separate in comparison");
var similarMine = ItemParser.Parse("Item Class: Rings\nRarity: Rare\nMine\nGold Ring\n--------\n+100 to maximum Life")!;
var similarOther = similarMine with { Details = similarMine.Details.Replace("+100", "+90") };
Check(SimilarItems.Score(similarMine, similarOther) == .9m, "similarity measures relative rolls");
Check(SimilarItems.Score(similarMine, similarOther with { Details = similarOther.Details + "\nTwice Corrupted" }) == 0, "similarity separates twice corruption");
Check(ItemAnalysis.From(similarOther with { Details = similarOther.Details + "\nTwice Corrupted" }).CorruptionLevel == 2, "twice corruption recognized");
var peerRows = Enumerable.Range(1,3).Select(i => new ComparableListing(new ImportedListing("peer"+i,"seller"+i,new ListingPrice(i*10,"Exalted Orb"),similarOther.Details,DateTimeOffset.UtcNow,true,true),similarOther,ItemAnalysis.From(similarOther))).ToArray();
var peerResult = new ComparableResult(peerRows,20,10,30,3,"Exalted Orb","fixture",DateTimeOffset.UtcNow);
Check(SimilarItems.Estimate(similarMine,peerResult).Price == 20, "similar sellers produce median recommendation");
Check(SimilarItems.Estimate(similarMine,peerResult with { Rows = peerRows.Take(2).ToArray() }).Price == null, "sparse similarity withholds recommendation");
Check(SimilarItems.Estimate(similarMine,peerResult with { Rows = peerRows.Select(r => r with { Listing = r.Listing with { Account = "same" } }).ToArray() }).Price == null, "same seller cannot inflate recommendation");
var displayMine = ItemParser.Parse("Item Class: Body Armours\nRarity: Unique\nWarden\nPrimal Markings\n--------\nQuality: +20%\nEvasion Rating: 1338\nItem Level: 84\n--------\nA gift from the darkness.\n--------\nCorrupted")!;
var displaySeller = ItemParser.Parse("Rarity: Unique\nWarden\nPrimal Markings\n--------\nItem Level: 81\nBody Armour\nQuality: +20%\nEvasion Rating: 1155\nCorrupted\n--------\n{ Flavour Text }\nA gift from the darkness.")!;
var minePresentation = ItemPresentation.Lines(displayMine);
var sellerPresentation = ItemPresentation.Lines(displaySeller);
Check(minePresentation[0].Text == "Body Armour" && sellerPresentation[0].Text == "Body Armour" && sellerPresentation[0].Kind == "Class", "clipboard and trade class labels share presentation");
Check(minePresentation.Select(l => l.Kind).SequenceEqual(sellerPresentation.Select(l => l.Kind)), "clipboard and trade sections have the same ordering");
Check(sellerPresentation[1].Text.StartsWith("Quality:") && sellerPresentation[^1].Kind == "Flavour" && sellerPresentation[^2].Text == "Corrupted", "properties and corruption use canonical order");
var desecratedCopy = ItemParser.Parse("Rarity: Rare\nHate Twine\nObliterator Bow\n--------\n{ Desecrated Prefix Modifier (Tier: 1) }\n+2 to Level of all Attack Skills")!;
var desecratedTrade = ItemParser.Parse("Rarity: Rare\nHate Twine\nObliterator Bow\n--------\n+2 to Level of all Attack Skills (desecrated)")!;
Check(ItemAnalysis.From(desecratedCopy).Desecrated && ItemAnalysis.From(desecratedTrade).Desecrated, "clipboard and trade preserve desecrated status");
Check(ItemAnalysis.From(desecratedTrade).Lines[0].Text == "+2 to Level of all Attack Skills" && ItemAnalysis.From(desecratedTrade).Lines[0].Kind == ItemAnalysis.From(desecratedCopy).Lines[0].Kind, "desecrated display marker does not alter stat matching");
var craftedCopy = ItemParser.Parse("Rarity: Rare\nHate Twine\nObliterator Bow\n--------\n{ Crafted Prefix Modifier }\n+2 to Level of all Attack Skills")!;
var craftedTrade = ItemParser.Parse("Rarity: Rare\nHate Twine\nObliterator Bow\n--------\n+2 to Level of all Attack Skills (crafted)")!;
Check(ItemAnalysis.From(craftedCopy).Lines[0].IsCrafted && ItemAnalysis.From(craftedTrade).Lines[0].IsCrafted, "crafted metadata retained from clipboard and trade");
Check(!ItemAnalysis.From(craftedCopy).Desecrated && !ItemAnalysis.From(craftedTrade).Desecrated, "crafted modifier does not mark item desecrated");
foreach (var modifier in new[] { "crafted", "desecrated", "fractured", "mutated", "vestigial", "bonded", "scourge", "crucible", "utility", "cosmetic" })
{
    var marked = ItemParser.Parse($"Rarity: Rare\nTest\nGold Ring\n--------\n+12 to maximum Life ({modifier})")!;
    var markedLine = ItemAnalysis.From(marked).Lines[0];
    Check(ItemMetadata.Type(markedLine).Equals(modifier,StringComparison.OrdinalIgnoreCase) && markedLine.Text == "+12 to maximum Life", $"metadata presentation for {modifier}");
}
Check(ItemMetadata.Styles.Select(s=>s.Name).Distinct().Count() == ItemMetadata.Styles.Count, "metadata palette has unique types");
Check(ItemMetadata.Type(new ItemLine("Future effect","Item text","Unknown metadata: future")) == "Unknown", "unknown metadata uses explicit fallback");
var lineage = ItemParser.Parse("Item Class: Support Gems\nRarity: Gem\nTacati's Ire\n--------\nSupport, Lineage, Chaos\n--------\nCategory: Tacati's Ire\nRequires: Level 65\nSupport Requirements: +5 Dex\n--------\nSupports Skills which can cause Damaging Hits. Poison inflicted with Supported Skills deals Damage faster the higher your Rage.\n--------\nPoisons from Supported Skills deal Damage 2% faster per Rage\n--------\nHe almost saved the Vaal. His unique poison made it past\nthe Queen's cupbearers; he had only to direct his anger...\nbut in her presence, he could feel naught but lust.\n--------\nPlace into a Skill's Support Gem socket in the Skills Panel to apply its effects to that Skill.")!;
var lineageLines = ItemAnalysis.From(lineage).Lines;
Check(ItemPresentation.IsGem(lineage) && lineageLines.Any(l=>l.Kind == "Gem description") && lineageLines.Count(l=>l.Kind == "Flavour") == 3 && lineageLines.Last().Kind == "Instructions", "lineage gem sections retain their presentation roles");
Check(lineageLines.Single(l=>l.Text.StartsWith("Poisons from")).Kind == "Item text", "lineage effect remains a modifier");
Check(ItemMetadata.Style(new ItemLine("+6 to all Attributes","Implicit")).Colour == "#A3A4F2", "ordinary implicits use in-game violet");
Check(ItemMetadata.Type(new ItemLine("+100 to maximum Life","Item text","Unique Modifier")) == "Explicit", "unique modifier metadata retains explicit colouring");

var socketFixture = JsonDocument.Parse("""{"sockets":[{"type":"rune"},{"type":"rune"}],"socketedItems":[{"socket":1,"name":"Iron Rune","socketedIcon":"https://web.poecdn.com/test.png"}]}""");
var actualSockets = ItemSockets.Parse(socketFixture.RootElement)!;
Check(actualSockets[0].Occupied == false && actualSockets[1].Name == "Iron Rune" && actualSockets[1].IconUrl != null, "socketed items map by index and preserve artwork");
var copiedSockets = ItemSockets.From(ItemParser.Parse("Rarity: Rare\nTest\nBow\n--------\nSockets: S S")!);
Check(copiedSockets.Count == 2 && copiedSockets.All(s=>s.Occupied == false), "clipboard sockets without augment effects show empty sockets");

var runeBow=ItemParser.Parse("Item Class: Bows\nRarity: Rare\nHate Twine\nObliterator Bow\n--------\nSockets: S S\n--------\n18% increased Physical Damage (rune)\nBow Attacks fire an additional Arrow (rune)")!;
var inferredRunes=ItemSockets.From(runeBow);
Check(inferredRunes[0].Name == "Greater Iron Rune" && inferredRunes[1].Name == "Countess Seske's Rune of Archery" && inferredRunes.All(s=>s.Inferred), "bow rune effects yield labelled inferred identities");
Check(ItemSockets.From(runeBow with { Sockets = new[] { new ItemSocket(0,"rune","Authoritative rune",null,true) } })[0].Name == "Authoritative rune", "source socket data takes precedence over inference");
Check(Economy.Category(lineage)?.Type == "LineageSupportGems" && Economy.UsesExchange(lineage) && !Economy.UsesEquipmentListings(lineage), "Lineage gems route exclusively to exchange prices");
var uncutExchange = ItemParser.Parse("Item Class: Uncut Skill Gems\nRarity: Currency\nUncut Skill Gem\n--------\nLevel: 19")!;
Check(Economy.Category(uncutExchange)?.Type == "UncutGems" && !Economy.UsesEquipmentListings(uncutExchange), "Uncut gems use their dedicated exchange feed");
var cutExchange = ItemParser.Parse("Item Class: Skill Gems\nRarity: Gem\nFireball\n--------\nLevel: 19")!;
Check(!Economy.UsesEquipmentListings(cutExchange) && Economy.Category(cutExchange) == null, "Unsupported cut gems never use equipment listings");
Check(Economy.UsesEquipmentListings(rare), "Rare equipment retains comparable listing search");
var levels = new EconomyRow[] { new("Uncut Skill Gem (Level 19)", "", "", 20, "Exalted Orb", null, null), new("Uncut Skill Gem (Level 20)", "", "", 200, "Exalted Orb", null, null) };
Check(Economy.Matching(levels, uncutExchange).Single().Value == 20, "Uncut exchange quotes match level, never a cheaper different level");
var lineageRows = Economy.Parse("""{"core":{"primary":"exalted","items":[{"id":"exalted","name":"Exalted Orb"},{"id":"tacati","name":"Tacati's Ire"}]},"lines":[{"id":"tacati","primaryValue":4.5}]}""",true);
Check(Economy.Matching(lineageRows,lineage).Single().Value == 4.5m, "Lineage exchange metadata resolves its market quote");
var adeptSocket = ItemSockets.From(ItemParser.Parse("Item Class: Gloves\nRarity: Rare\nHate Vise\nGrand Bracers\n--------\nSockets: S\n--------\n+12 to Dexterity (rune)")!).Single();
Check(adeptSocket.Name == "Greater Adept Rune" && adeptSocket.Inferred, "Copied Dexterity rune resolves its name");
var combinedSockets = ItemSockets.From(ItemParser.Parse("Item Class: Body Armours\nRarity: Unique\nForgotten Warden\nPrimal Markings\n--------\nSockets: S S\n--------\n36% increased Armour, Evasion and Energy Shield (rune)")!);
Check(combinedSockets.Count == 2 && combinedSockets.All(s => s.Name == "Iron Rune family" && s.Inferred && s.Details!.Contains("16% + 20%")), "Combined defence effects show ambiguous rune combinations without inventing tiers");
var bookmarkPath = Path.Combine(Path.GetTempPath(), "AffixPrism-bookmarks-" + Guid.NewGuid(), "items.json");
try {
    var bookmarks = new ItemBookmarks(bookmarkPath);
    bookmarks.Save(runeBow,null); bookmarks.Save(runeBow,null);
    var reloaded = new ItemBookmarks(bookmarkPath);
    Check(reloaded.Read().Count == 1 && reloaded.Read()[0].Item.Details == runeBow.Details, "Bookmarks persist complete item snapshots without duplicates");
    reloaded.Remove(reloaded.Read()[0]);
    Check(new ItemBookmarks(bookmarkPath).Read().Count == 0, "Bookmark removal persists");
} finally { File.Delete(bookmarkPath); Directory.Delete(Path.GetDirectoryName(bookmarkPath)!); }
var baseRequest = new ComparableRequest(rare, "Standard", "Exalted Orb", Array.Empty<PriceConstraint>(), false, false, null);
using(var exactQuery = JsonDocument.Parse(LiveTradeClient.BuildQuery(baseRequest,null)))
    Check(exactQuery.RootElement.GetProperty("query").GetProperty("type").GetString() == rare.BaseType, "Exact base selection reaches live query");
using(var broadQuery = JsonDocument.Parse(LiveTradeClient.BuildQuery(baseRequest with { ExactBase = false },null)))
    Check(!broadQuery.RootElement.GetProperty("query").TryGetProperty("type",out _), "Disabling base filter removes exact type restriction");
var defaultBow = DefaultItemFilters.Suggested(runeBow);
Check(defaultBow.Count <= 3 && !defaultBow.Contains("18% increased Physical Damage"), "Default filters exclude socket effects and cap the starting selection");
var defaultRing = ItemParser.Parse("Item Class: Rings\nRarity: Rare\nTest Ring\nGold Ring\n--------\n+100 to maximum Life\n+25% to Fire Resistance")!;
Check(DefaultItemFilters.Suggested(defaultRing).Contains("+100 to maximum Life"), "Rare item defaults prioritize life over minor resistance rolls");
Check(DefaultItemFilters.Suggested(lineage).Count == 0, "Exchange gems do not receive equipment defaults");
using(var autoQuery = JsonDocument.Parse(LiveTradeClient.BuildQuery(baseRequest with { Currency = "Auto" },null)))
    Check(!autoQuery.RootElement.GetProperty("query").GetProperty("filters").GetProperty("trade_filters").GetProperty("filters").TryGetProperty("price",out _), "Auto currency does not restrict listing currency");
var autoNow=DateTimeOffset.UtcNow;
var autoDoc = new ListingDocument("Standard","fixture",autoNow,new[] { new ImportedListing("a","seller-a",new(1,"Divine Orb"),rare.Details,autoNow,true,false), new ImportedListing("b","seller-b",new(20,"Exalted Orb"),rare.Details,autoNow,true,false), new ImportedListing("c","seller-c",new(30,"Exalted Orb"),rare.Details,autoNow,true,false) });
var autoResult=ComparableMarket.Parse(JsonSerializer.Serialize(autoDoc),autoNow).Search(baseRequest with { Currency="Auto" },autoNow);
Check(autoResult.Rows.Count==3 && autoResult.Currency=="Exalted Orb" && autoResult.Median==null, "Auto retains real mixed-currency prices and never mixes currencies into a median");
var skillBound = new EvaluationFilter(new ItemLine("+4 to Level of all Melee Skills","Explicit")) { Enabled=true };
skillBound.Preset(true);
Check(skillBound.Minimum == "4", "Broad skill-level bound rounds up to equivalent integer minimum");
skillBound.Minimum="3.6";
Check(skillBound.Validate()!=null,"Fractional skill-level bounds rejected");
var criticalBound=new EvaluationFilter(new ItemLine("+1.17% to Critical Hit Chance","Explicit")); criticalBound.Preset(true);
foreach (var text in new[] { "Item Level: 82", "Requires: Level 72, 115 Dex, 46 Int", "Quality: +20%", "+12 to Level of all Melee Skills", "10 uses remaining", "-20% to Fire Resistance", "2 additional Projectiles" })
{
    var fixedBound = new EvaluationFilter(new ItemLine(text,"Property")); fixedBound.Preset(true);
    Check(!fixedBound.AllowsBroad && fixedBound.Minimum == fixedBound.CopiedValue?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), "Broad preserves fixed stat: " + text);
}
Check(criticalBound.Minimum=="1.05","Fractional critical chance retains decimal broad bounds");
var dpsItem=ItemParser.Parse("Item Class: Bows\nRarity: Rare\nDPS Test\nTest Bow\n--------\nPhysical Damage: 100-200\nElemental Damage: 10-30 (fire), 20-40 (cold)\nAttacks per Second: 2.00")!;
var dpsLines=ItemAnalysis.From(dpsItem).Lines;
Check(dpsLines.Any(l=>l.Text=="Physical DPS: 300") && dpsLines.Any(l=>l.Text=="Elemental DPS: 100") && dpsLines.Any(l=>l.Text=="Total DPS: 400"), "DPS averages damage ranges and applies attack rate");
var resistanceItem=ItemParser.Parse("Item Class: Rings\nRarity: Rare\nResist Test\nGold Ring\n--------\n+10% to all Elemental Resistances\n+20% to Fire Resistance\n+5% to Cold and Lightning Resistances\n+99% to Fire Resistance while Moving")!;
Check(ItemAnalysis.From(resistanceItem).Lines.Any(l=>l.Kind=="Pseudo" && l.Text=="60% total Elemental Resistance"), "Pseudo resistance sums all/dual rolls and excludes conditional rolls");
var skillDpsItem=ItemParser.Parse(dpsItem.Details + "\n--------\n+2 to Level of all Attack Skills")!;
var weaponDefaults=DefaultItemFilters.Suggested(skillDpsItem);
Check(weaponDefaults.Contains("Total DPS: 400") && weaponDefaults.Contains("+2 to Level of all Attack Skills"), "Attack weapon defaults require output as well as skill levels");
var weakerDpsItem=ItemParser.Parse(skillDpsItem.Details.Replace("100-200","50-100"))!;
var weaponNow=DateTimeOffset.UtcNow;
var weaponDoc=new ListingDocument("Standard","fixture",weaponNow,new[] { new ImportedListing("weak","seller",new(1,"Chaos Orb"),weakerDpsItem.Details,weaponNow,true,false) });
var weaponResult=ComparableMarket.Parse(JsonSerializer.Serialize(weaponDoc),weaponNow).Search(new(skillDpsItem,"Standard","Auto",new[] {new PriceConstraint("Total DPS: 400",360,null,0,"Property"),new PriceConstraint("+2 to Level of all Attack Skills",2,null,0,"Item text",1)},false,false,null),weaponNow);
Check(weaponResult.Rows.Count==0,"Lower-DPS weapon does not qualify just by sharing skill levels");
var bootDefaults=DefaultItemFilters.Suggested(ItemParser.Parse("Item Class: Boots\nRarity: Rare\nTest Boots\nLeather Boots\n--------\n30% increased Movement Speed\n+100 to maximum Life\n+35% to Fire Resistance\n+60 to maximum Mana")!);
Check(bootDefaults.Count==3 && bootDefaults[0]=="30% increased Movement Speed" && bootDefaults.Contains("+100 to maximum Life") && bootDefaults.Any(t=>t.Contains("total Elemental Resistance")), "Boot defaults prioritize movement, life and aggregate resistance");
var attackDefaults=DefaultItemFilters.Suggested(skillDpsItem);
Check(attackDefaults.Count==3 && attackDefaults.Contains("Total DPS: 400") && attackDefaults.Contains("Attacks per Second: 2.00"), "Weapon defaults select DPS, skill level and speed without duplicate damage filters");
var conversionSnapshot=new EconomySnapshot(new[] {new EconomyRow("Divine Orb","","",100,"Exalted Orb",null,null),new EconomyRow("Chaos Orb","","",2,"Exalted Orb",null,null)},DateTimeOffset.UtcNow,false,false);
var conversionRates=PriceConversion.Rates(conversionSnapshot,"Divine Orb");
Check(conversionRates["Exalted Orb"]==.01m && conversionRates["Chaos Orb"]==.02m,"Cross currency rates use common primary without relabelling amounts");
Check(PriceConversion.Rates(conversionSnapshot with { Stale=true },"Divine Orb").Count==0,"Stale rates never feed cross currency estimate");
var mixedPeers=new[] { new ComparableListing(new ImportedListing("one","one",new(1,"Divine Orb"),defaultRing.Details,DateTimeOffset.UtcNow,true,false),defaultRing,ItemAnalysis.From(defaultRing)),new ComparableListing(new ImportedListing("two","two",new(100,"Exalted Orb"),defaultRing.Details,DateTimeOffset.UtcNow,true,false),defaultRing,ItemAnalysis.From(defaultRing)),new ComparableListing(new ImportedListing("three","three",new(50,"Chaos Orb"),defaultRing.Details,DateTimeOffset.UtcNow,true,false),defaultRing,ItemAnalysis.From(defaultRing)) };
var mixedEstimate=SimilarItems.Estimate(defaultRing,new ComparableResult(mixedPeers,null,null,null,3,"Divine Orb","fixture",DateTimeOffset.UtcNow,ExchangeRates:conversionRates));
Check(mixedEstimate.Price==1 && mixedEstimate.Sellers==3 && mixedPeers[1].Listing.Price.Amount==100,"Closest-item estimate pools converted independent sellers while preserving original prices");
Check(SimilarItems.Score(skillDpsItem,ItemParser.Parse(skillDpsItem.Details.Replace("+2 to Level","+1 to Level"))!)==0,"Different skill level cannot hide inside a high average similarity");
var pseudoFixture=PseudoStats.Calculate(new[] {new ItemLine("+10 to all Attributes","Implicit"),new ItemLine("+20 to Strength and Dexterity","Item text"),new ItemLine("+100 to maximum Life","Item text"),new ItemLine("+40 to maximum Mana","Item text"),new ItemLine("+12% to Fire and Chaos Resistances","Item text")});
Check(pseudoFixture.Any(l=>l.Text=="+160 total maximum Life") && pseudoFixture.Any(l=>l.Text=="+60 total maximum Mana"),"Pseudo life and mana include attribute contribution exactly once");
Check(pseudoFixture.Any(l=>l.Text=="+30 total to Dexterity") && pseudoFixture.Any(l=>l.Text=="12% total to Chaos Resistance"),"Pseudo attributes and hybrid chaos resistance aggregate correctly");
var notePath=Path.Combine(Path.GetTempPath(),"AffixPrism-note-"+Guid.NewGuid(),"notes.json");
try { var store=new ItemBookmarks(notePath); store.Save(defaultRing,null); store.UpdateNotes(store.Read()[0],"Compare after crafting"); store.Save(defaultRing,null); Check(new ItemBookmarks(notePath).Read()[0].Notes=="Compare after crafting","Rebookmarking retains persisted notes"); }
finally { File.Delete(notePath); Directory.Delete(Path.GetDirectoryName(notePath)!); }
await LiveTradeChecks.Run(Check);
Check(SocketAugments.Match("Body Armours","Skills have 10% chance to not remove Charges but still count as consuming them") == "Idol of Eramir", "Socket catalogue resolves body armour idol");
Check(SocketAugments.Match("Bows","Skills have 10% chance to not remove Charges but still count as consuming them") == null, "Socket catalogue respects equipment category");
Check(SocketAugments.Icon("Idol of Eramir")?.StartsWith("https://web.poecdn.com/") == true, "Socket catalogue provides trusted artwork without market request");
Check(new SocketArtworkCatalog().Resolve(new ItemSocket(0,"rune","Greater Iron Rune",null,true)).IconUrl != null, "Named trade socket resolves bundled artwork");
Check(new SocketArtworkCatalog().Resolve(new ItemSocket(0,"rune","Unrecognised socket",null,true)).IconUrl == null, "Unknown sockets never borrow artwork");
Check(ItemAnalysis.From(ItemParser.Parse("Item Class: Quivers\nRarity: Rare\nHavoc Barb\nSacral Quiver\n--------\n+22 to Dexterity\n--------\nCan only be equipped if you are wielding a Bow.")!).Lines.Any(l=>l.Text.StartsWith("Can only be equipped") && l.Kind=="Description"), "Copied quiver equip restriction uses description styling");
var jewelDescription = ItemAnalysis.From(ItemParser.Parse("Item Class: Jewels\nRarity: Magic\nPerforating Emerald\nEmerald\n--------\nItem Level: 79\n--------\n11% increased Damage with Bows\n--------\nPlace into an allocated Jewel Socket on the Passive Skill Tree. Right click\nto remove from the Socket.")!);
Check(jewelDescription.Lines.Count(l=>l.Kind=="Description")==2 && jewelDescription.Lines.Any(l=>l.Text=="11% increased Damage with Bows" && l.Kind=="Item text"), "Jewel instructions and wrapped continuation are descriptions, while modifiers remain modifiers");
Check(ItemPresentation.IsClassLabel("Jewel"), "Trade jewel class label is not a modifier");
var essenceText = ItemAnalysis.From(ItemParser.Parse("Item Class: Stackable Currency\nRarity: Currency\nGreater Essence of the Body\n--------\nStack Size: 1/10\n--------\nUpgrades a Magic item to a Rare item, adding a guaranteed modifier\nBelt, Body Armour, Helmet or Shield: +(100-119) to maximum Life\nAmulet, Boots or Gloves: +(85-99) to maximum Life\n--------\nRight click this item then left click a Magic item to apply it.")!);
Check(essenceText.Lines.Count(l=>l.Kind=="Explicit")==3 && essenceText.Lines.Any(l=>l.Kind=="Instructions") && essenceText.Lines.Any(l=>l.Text=="Stack Size: 1/10" && l.Kind=="Property"), "Essence crafting effects are purple, use instructions separate, stack remains a property");
Check(MarketPriceDisplay.FormatQuantity(inexpensive,100,displayRates,displayNow)=="500 Exalted Orb", "Stack uses the same currency as its unit price across divine threshold");
Check(MarketPriceDisplay.FormatQuantity(inexpensive,100,displayRates with {Stale=true},displayNow)=="2 Divine Orb", "Stack conversion rejects stale exchange rates");
var usage = ItemAnalysis.From(ItemParser.Parse("Item Class: Tablets\nRarity: Magic\nOverseer Tablet\n--------\n28% increased Gold found in Map\n--------\nCan be used in a personal Map Device\nto add modifiers to a Map.")!);
Check(usage.Lines.Count(l=>l.Kind=="Instructions")==2 && usage.Lines.Any(l=>l.Kind=="Item text"), "Wrapped tablet usage stays outside modifiers");
Check(SimilarItems.Explain(similarMine,similarOther).Contains("weighted stat similarity"), "Comparison explains weighted qualification");
Check(SimilarItems.Explain(skillDpsItem,ItemParser.Parse(skillDpsItem.Details.Replace("+2 to Level","+1 to Level"))!).Contains("skill levels"), "Comparison explains critical stat exclusion");
var weightedRing=ItemParser.Parse("Item Class: Rings\nRarity: Rare\nExample\nGold Ring\n--------\n+100 to maximum Life\n+30 to maximum Mana\n+20 to all Attributes\n+12 to Accuracy Rating")!;
var secondaryPeer=ItemParser.Parse(weightedRing.Details.Replace("+12 to Accuracy Rating","+10 to Dexterity"))!;
Check(SimilarItems.Score(weightedRing,secondaryPeer)>=.8m,"Matching key ring stats tolerate one different secondary stat");
var importantPeer=ItemParser.Parse(weightedRing.Details.Replace("+100 to maximum Life","+10 to maximum Life"))!;
Check(SimilarItems.Score(weightedRing,importantPeer)<.8m,"A major loss of a key ring stat fails valuation threshold");
Check(SimilarItems.Score(weightedRing,weightedRing with {BaseType="Iron Ring"})==0,"Non-weapon bases remain distinct");
var overviewSame=ComparisonOverview.Create(skillDpsItem,skillDpsItem,"Physical attacks");
Check(overviewSame.Verdict.StartsWith("Very similar"),"Overview identical weapon is similar");
var overviewLoss=ComparisonOverview.Create(skillDpsItem,ItemParser.Parse(skillDpsItem.Details.Replace("+2 to Level","+1 to Level"))!,"Physical attacks");
Check(overviewLoss.Losses.Length>0 && !overviewLoss.Verdict.StartsWith("Other item"),"Overview cannot call a skill-level loss an upgrade");
Check(ComparisonOverview.Create(skillDpsItem,skillDpsItem with {Details=skillDpsItem.Details+"\nUnidentified"},"Physical attacks").Verdict.StartsWith("Insufficient"),"Overview never ranks hidden unidentified stats");
Check(new OverviewStat("Critical Hit Chance",11.17m,13.53m,true).Difference=="+2.36 points","Crit change is percentage points");
Check(new OverviewStat("Total DPS",100,150).Difference=="+50%","DPS comparison uses relative difference");
var fixedRollItem=ItemParser.Parse("Item Class: Wands\nRarity: Unique\nAdonia's Ego\nSiphoning Wand\n--------\n+4(3) to Level of all Spell Skills\n-11(-10)% to all Elemental Resistances per Power Charge\n--------\nCorrupted")!;
var fixedRollAnalysis=ItemAnalysis.From(fixedRollItem);
Check(fixedRollAnalysis.Lines.Any(l=>l.Text=="+4 to Level of all Spell Skills" && l.Rolls!.Single().Value==4 && l.Rolls.Single().Minimum==3),"Modified fixed skill roll keeps actual value and reference metadata");
Check(fixedRollAnalysis.Lines.Any(l=>l.Text=="-11% to all Elemental Resistances per Power Charge" && l.Rolls!.Single().Minimum==-10),"Negative fixed roll removes reference from filter text");
Check(ComparableMarket.Signature(fixedRollAnalysis.Lines.First().Text)==ComparableMarket.Signature("+3 to Level of all Spell Skills"),"Fixed roll maps to ordinary trade stat signature");
var radiusItem=ItemParser.Parse("Item Class: Jewels\nRarity: Rare\nKraken Spark\nTime-Lost Ruby\n--------\nUpgrades Radius to Medium — Unscalable Value\n15% increased Effect of Notable Passive Skills in Radius — Unscalable Value")!;
var radiusLines=ItemAnalysis.From(radiusItem).Lines;
Check(radiusLines[0].Text=="Upgrades Radius to Medium", "Radius presence filter removes unscalable annotation");
var unscaledFilter=new EvaluationFilter(radiusLines[1]); unscaledFilter.Preset(true);
Check(unscaledFilter.Minimum=="15" && !unscaledFilter.AllowsBroad,"Unscalable numeric roll stays exact in broad mode");
using(var radiusCatalogue=JsonDocument.Parse("{\"result\":[{\"entries\":[{\"id\":\"explicit.radius\",\"text\":\"Upgrades Radius to Medium\"}]}]}"))
{
 var request=new ComparableRequest(radiusItem,"Demo","Auto",new[]{new PriceConstraint(radiusLines[0].Text,null,null,Kind:radiusLines[0].Kind)},false,false,null);
 Check(LiveTradeClient.BuildQuery(request,radiusCatalogue.RootElement).Contains("explicit.radius"),"Radius modifier builds a presence-only trade query");
 var doc=new ListingDocument("Demo","fixture",DateTimeOffset.UtcNow,new[]{new ImportedListing("radius","seller",new ListingPrice(1,"Divine Orb"),radiusItem.Details,DateTimeOffset.UtcNow,true,true)});
 var market=ComparableMarket.Parse(JsonSerializer.Serialize(doc),DateTimeOffset.UtcNow);
 Check(market.Search(request,DateTimeOffset.UtcNow).Rows.Count==1,"Presence-only modifier survives local listing filtering");
}
Check(SocketAugments.Match("Wands","+1 to Level of all Spell Skills")=="Hedgewitch Assandra's Rune of Wisdom","Positive clipboard sign resolves wand rune catalogue");
Check(SocketAugments.Match("Wands","-1 to Level of all Spell Skills")==null,"Socket normalization preserves negative signs");
var wandSocketItem=ItemParser.Parse("Item Class: Wands\nRarity: Unique\nAdonia's Ego\nSiphoning Wand\n--------\nSockets: S\n--------\n+1 to Level of all Spell Skills (rune)")!;
var wandSocket=new SocketArtworkCatalog().Resolve(ItemSockets.From(wandSocketItem).Single());
Check(wandSocket.Name=="Hedgewitch Assandra's Rune of Wisdom" && wandSocket.IconUrl!=null && wandSocket.Inferred,"Copied wand resolves rune identity and artwork while preserving inference label");
var ventor=ItemParser.Parse("Item Class: Rings\nRarity: Unique\nVentor's Gamble\nGold Ring\n--------\n12% increased Rarity of Items found (implicit)\n+37 to maximum Life\n+20 to Spirit\n9% increased Rarity of Items found\n-11% to Cold Resistance")!;
var ventorOther=ItemParser.Parse(ventor.Details.Replace("+37","+79").Replace("+20","+19").Replace("9% increased","10% increased").Replace("-11%","+35%"))!;
var generalRing=ComparisonOverview.Create(ventor,ventorOther,"General");
Check(generalRing.Stats.Count==5 && generalRing.Stats.Any(s=>s.Name.Contains("Spirit") && s.Direction<0),"General ring overview includes spirit and all numeric modifiers");
Check(generalRing.Stats.Count(s=>s.Name.Contains("Rarity"))==2,"General comparison keeps implicit and explicit rarity separate");
Check(generalRing.Stats.Any(s=>s.Yours==-11 && s.Other==35 && s.Direction>0) && generalRing.Verdict.StartsWith("Trade-off"),"General comparison preserves negative resistances and mixed outcomes");
var foundationItem=ItemParser.Parse("Item Class: Body Armours\nRarity: Unique\nForgotten Warden\nPrimal Markings\n--------\nSockets: S S S\n--------\n+30 to Armour (rune)\n+30 to Evasion Rating (rune)\n+10 to maximum Energy Shield (rune)\n12% increased Rarity of Items found (rune)\nSkills have 10% chance to not remove Charges but still count as consuming them (rune)")!;
var foundationSockets=ItemSockets.From(foundationItem);
Check(foundationSockets.Count==3 && foundationSockets.Select(s=>s.Name).Order().SequenceEqual(new[]{"Idol of Eramir","Rabbit Idol","Rune of Foundations"}.Order()),"Multi-line Foundations effect resolves three socket identities");
Check(foundationSockets.All(s=>new SocketArtworkCatalog().Resolve(s).IconUrl!=null && s.Inferred),"Grouped sockets have artwork and remain labelled inferred");
Check(SocketAugments.MatchGroups("Body Armours",new[]{"+30 to Armour","+30 to Evasion Rating"},1)==null,"Incomplete multi-line rune is not falsely identified");
var runeCatalog = new[] { new EconomyRow("Greater Iron Rune","","",5,"Exalted Orb",null,null),new EconomyRow("Perfect Iron Rune","","",50,"Exalted Orb",null,null),new EconomyRow("Iron Rune","","",1,"Exalted Orb",null,null), new EconomyRow("Countess Seske's Rune of Archery","","",80,"Exalted Orb",null,null) };
Check(RuneNames.Match("GREATER IRON RUNE",90,runeCatalog)?.Row.Value == 5, "Rune OCR exact names preserve tier");
Check(RuneNames.Match("2x Greater Iron Rune",90,runeCatalog)?.PriceLabel.Contains("10 Exalted") == true, "Rune choices include stack total");
Check(RuneNames.Match("Greater Iron Rune",30,runeCatalog) == null, "Low confidence OCR never shows a price");
Check(RuneNames.Match("Greater lron Rune",90,runeCatalog)?.Approximate == true, "Small OCR errors are labelled approximate");
Check(RuneNames.Match("Perfect Iron Rune",90,runeCatalog.Take(1).ToArray()) == null, "Missing rune tier cannot fall back to another tier");
Check(RuneNames.Match("Choose your reward",99,runeCatalog) == null, "Unrelated menu text has no guessed price");
Check(RuneNames.Match("Iron Rune",90,runeCatalog.Concat(new[] {runeCatalog[2]}).ToArray()) == null, "Ambiguous market identities are rejected");
await ReliabilityChecks.Run(Check);
var migrationRoot=Path.Combine(Path.GetTempPath(),"AffixPrism-migration-test-"+Guid.NewGuid().ToString("N"));
try
{
    string oldData=Path.Combine(migrationRoot,LegacyMigration.PreviousName), newData=Path.Combine(migrationRoot,"AffixPrism");
    Directory.CreateDirectory(Path.Combine(oldData,"windows")); Directory.CreateDirectory(Path.Combine(oldData,"updates"));
    File.WriteAllText(Path.Combine(oldData,"settings.json"),"original settings"); File.WriteAllText(Path.Combine(oldData,"windows","layout.json"),"layout"); File.WriteAllText(Path.Combine(oldData,"updates","pending.json"),"obsolete job");
    Check(LegacyMigration.Import(migrationRoot) && File.ReadAllText(Path.Combine(newData,"settings.json"))=="original settings" && File.Exists(Path.Combine(newData,"windows","layout.json")),"Brand migration preserves settings and window history");
    Check(!Directory.Exists(Path.Combine(newData,"updates")) && File.Exists(Path.Combine(oldData,"settings.json")),"Brand migration skips old updater jobs and leaves originals intact");
    File.WriteAllText(Path.Combine(newData,"settings.json"),"new settings");
    Check(!LegacyMigration.Import(migrationRoot) && File.ReadAllText(Path.Combine(newData,"settings.json"))=="new settings","Brand migration never overwrites the new profile");
}
finally { if(Directory.Exists(migrationRoot))Directory.Delete(migrationRoot,true); }
Console.WriteLine($"{checks} checks passed.");

sealed class CaptureFake : IItemCapturePlatform
{
    public nint Game = 1;
    public bool Released = true, UpdateClipboard = true, LoseFocusOnCopy;
    public int CopyCalls, BusyReads;
    public string Text = "";
    public nint ForegroundGame => Game;
    public bool ShortcutKeysReleased => Released;
    public uint ClipboardVersion { get; private set; } = 10;
    public Task<bool> SendCopyAsync(CancellationToken cancellationToken) { CopyCalls++; if (UpdateClipboard) ClipboardVersion++; if (LoseFocusOnCopy) Game = 0; return Task.FromResult(true); }
    public string? ReadItemText(nint gameWindow) => BusyReads-- > 0 ? null : Text;
}

sealed class KeyFake : ICopyKeySender
{
    private readonly System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
    public List<(string Key, long Time)> Events = new();
    public bool FailCopyDown, LoseFocusAfterControl;
    public Action<ushort>? AfterKey;
    public bool HasGameFocus { get; private set; } = true;
    public bool SendKey(ushort key, bool down)
    {
        Events.Add(($"{key}:{down}", watch.ElapsedMilliseconds));
        if (down) AfterKey?.Invoke(key);
        if (key == 17 && down && LoseFocusAfterControl) HasGameFocus = false;
        return !(key == 67 && down && FailCopyDown);
    }
}

sealed class MarketHandler(string json) : HttpMessageHandler
{
    public int Calls; public bool Fail;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Calls++;
        return Task.FromResult(new HttpResponseMessage(Fail ? System.Net.HttpStatusCode.TooManyRequests : System.Net.HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
