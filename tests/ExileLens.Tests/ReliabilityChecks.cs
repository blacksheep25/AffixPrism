using ExileLens.Core;
using System.Net;
using System.Text.Json;

static class ReliabilityChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        CopiedItem Read(string name)=>ItemParser.Parse(File.ReadAllText("tests/Fixtures/"+name))!;
        var bow=Read("desecrated-bow.txt");
        var lines=ItemAnalysis.From(bow).Lines;
        check(lines.Any(l=>l.IsCrafted && l.Text.Contains("Level of all Attack")) && lines.Any(l=>l.IsDesecrated && l.Text.StartsWith("Companions")),"Reported bow retains crafted and desecrated modifier metadata");
        var sockets=ItemSockets.From(bow);
        check(sockets.Count==2 && sockets.All(s=>s.Inferred) && sockets[1].Name=="Countess Seske's Rune of Archery","Reported bow identifies possible rune names without claiming confirmed socket contents");
        var gem=Read("lineage-gem.txt");
        check(Economy.UsesExchange(gem) && Economy.Category(gem)?.Type=="LineageSupportGems","Reported lineage gem routes to exchange category");
        check(ItemAnalysis.From(gem).Lines.Any(l=>l.Kind=="Gem description") && ItemAnalysis.From(gem).Lines.Any(l=>l.Kind=="Flavour"),"Lineage description and lore retain separate presentation types");
        check(Economy.UsesExchange(Read("currency-stack.txt")),"Currency stack routes to exchange instead of equipment search");
        using var socketJson=JsonDocument.Parse(File.ReadAllText("tests/Fixtures/socket-source.json"));
        var confirmed=ItemSockets.Parse(socketJson.RootElement)!;
        check(confirmed.Count==3 && confirmed[2].Occupied==false && confirmed[0].IconUrl!.Contains("greater-iron"),"Source sockets retain preferred socketed artwork and explicit empty slot");
        var icons=new SocketArtworkCatalog();
        check(icons.Observe(confirmed) && icons.Resolve(sockets[0]).IconUrl==confirmed[0].IconUrl && icons.Resolve(sockets[0]).Inferred,"Known socket artwork enriches inferred exact name without changing confidence");
        check(icons.Resolve(sockets[0] with {Name="Iron Rune"}).IconUrl==null && icons.Resolve(sockets[0] with {Name="Iron Rune family"}).IconUrl==null,"Artwork never crosses tiers or resolves ambiguous family names");
        check(!icons.Observe(new[]{sockets[1] with {IconUrl="https://web.poecdn.com/fixture/inferred.png"}}) && !icons.Observe(new[]{confirmed[0] with {Name="Bad",IconUrl="file:///private.png"}}),"Artwork rejects inferred sources and non-official URLs");
        using var labels=JsonDocument.Parse(File.ReadAllText("tests/Fixtures/similarity-benchmark.json"));
        foreach(var test in labels.RootElement.EnumerateArray())
        {
            string before=test.GetProperty("replace").GetString()!,after=test.GetProperty("withText").GetString()!;
            var peer=ItemParser.Parse(before.Length==0 ? bow.Details+after : bow.Details.Replace(before,after))!;
            check((SimilarItems.Score(bow,peer)>=.8m)==test.GetProperty("eligible").GetBoolean(),"Similarity benchmark: "+test.GetProperty("name").GetString());
        }
        var now=DateTimeOffset.UtcNow;
        ComparableListing Row(string id,string seller,decimal amount,string currency)=>new(new(id,seller,new(amount,currency),bow.Details,now,true,false),bow,ItemAnalysis.From(bow));
        var result=new ComparableResult(new[]{Row("1","A",1,"Divine Orb"),Row("2","B",100,"Exalted Orb"),Row("3","C",50,"Chaos Orb"),Row("4","A",999,"Divine Orb")},null,null,null,3,"Divine Orb","fixture",now,TotalMatches:100,FetchedCount:30);
        var diagnostics=PriceDiagnostics.From(bow,result);
        check(diagnostics.SimilarSellers==1 && diagnostics.MissingRates==2 && diagnostics.PartialSample,"Diagnostics distinguish duplicate sellers, absent rates and partial coverage");
        check(SimilarItems.Estimate(bow,result).Price==null,"Missing rates cannot fabricate a benchmark price");
        result=result with {ExchangeRates=new Dictionary<string,decimal>{{"Exalted Orb",.01m},{"Chaos Orb",.02m}}};
        check(SimilarItems.Estimate(bow,result).Price==1 && SimilarItems.Estimate(bow,result).Sellers==3,"Benchmark median is invariant across converted currencies and duplicate sellers");

        var request=new ComparableRequest(bow,"Fixture league","Auto",Array.Empty<PriceConstraint>(),false,false,null);
        foreach(var test in new[]{(401,"sign-in"),(403,"sign-in"),(400,"catalogue"),(429,"rate limit"),(500,"unavailable"),(503,"unavailable")})
        {
            using var handler=new ScriptedHandler(_=>new HttpResponseMessage((HttpStatusCode)test.Item1){Content=new StringContent("{}")});
            using var http=new HttpClient(handler); var client=new LiveTradeClient(http);
            string error=""; try {await client.SearchAsync(request,CancellationToken.None);} catch(HttpRequestException ex){error=ex.Message;}
            check(error.Contains(test.Item2,StringComparison.OrdinalIgnoreCase) && handler.Calls==1,$"HTTP {test.Item1} gives actionable status without automatic retries");
            if(test.Item1==429) {try {await client.SearchAsync(request,CancellationToken.None);} catch(HttpRequestException){} check(handler.Calls==1,"Rate limit blocks immediate repeated network request");}
        }
        foreach(string body in new[]{"<html>Verification required</html>","[]","null"})
        {
            using var handler=new ScriptedHandler(_=>new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body)});
            using var http=new HttpClient(handler); bool rejected=false;
            try {await new LiveTradeClient(http).SearchAsync(request,CancellationToken.None);} catch(JsonException ex){rejected=ex.Message.Contains("Trade",StringComparison.OrdinalIgnoreCase);}
            check(rejected && handler.Calls==1,"Malformed/non-object response fails without invented prices: "+body[..Math.Min(12,body.Length)]);
        }
        int catalogs=0,searches=0;
        using var revised=new ScriptedHandler(path=> {
            if(path.Contains("/data/stats")) { catalogs++; return Ok("{\"result\":[{\"entries\":[{\"id\":\"explicit.life"+catalogs+"\",\"text\":\"+# to maximum Life\"}]}]}"); }
            searches++; return searches==1 ? new HttpResponseMessage(HttpStatusCode.BadRequest){Content=new StringContent("{}")} : Ok("{\"id\":\"empty\",\"total\":0,\"result\":[]}");
        });
        using var revisedHttp=new HttpClient(revised); var revisedClient=new LiveTradeClient(revisedHttp);
        var ring=ItemParser.Parse("Rarity: Rare\nFixture Ring\nGold Ring\n--------\n+100 to maximum Life")!;
        var filtered=request with {Item=ring,Filters=new[]{new PriceConstraint("+100 to maximum Life",90,null,0,"Item text")}};
        try {await revisedClient.SearchAsync(filtered,CancellationToken.None);} catch(HttpRequestException){}
        await revisedClient.SearchAsync(filtered,CancellationToken.None);
        check(catalogs==2 && searches==2 && revised.Bodies.Last().Contains("explicit.life2"),"Manual search after rejection reloads revised catalogue and uses new stat ID");
    }
    private static HttpResponseMessage Ok(string body)=>new(HttpStatusCode.OK){Content=new StringContent(body)};
    private sealed class ScriptedHandler(Func<string,HttpResponseMessage> reply):HttpMessageHandler
    {
        public int Calls; public List<string> Bodies=new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        { Calls++; if(request.Content!=null) Bodies.Add(await request.Content.ReadAsStringAsync(token)); return reply(request.RequestUri!.AbsolutePath); }
    }
}
