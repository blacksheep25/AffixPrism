using AffixPrism.Core;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(server => { server.ListenLocalhost(47921); server.Limits.MaxRequestBodySize = 10 * 1024 * 1024; });
var app = builder.Build();
var markets = new ConcurrentDictionary<string, ComparableMarket>(StringComparer.Ordinal);
var directory = builder.Configuration["data-dir"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AffixPrism", "market-service");
Directory.CreateDirectory(directory);
foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
{
    try
    {
        if (new FileInfo(file).Length > 10 * 1024 * 1024) continue;
        var market = ComparableMarket.Parse(await File.ReadAllTextAsync(file), DateTimeOffset.UtcNow);
        markets[market.League] = market;
    }
    catch (Exception ex) when (ex is IOException or JsonException) { app.Logger.LogWarning("Ignored invalid or expired listing dataset {File}", Path.GetFileName(file)); }
}
// Loopback only, no CORS, reject browser-originated writes and require a custom header.
app.Use(async (context, next) =>
{
    if (context.Request.Headers.ContainsKey("Origin") || context.Request.Host.Host is not ("localhost" or "127.0.0.1" or "[::1]")) { context.Response.StatusCode = 403; return; }
    if (context.Request.Method != "GET" && context.Request.Headers["X-AffixPrism-Client"] != "desktop") { context.Response.StatusCode = 403; return; }
    await next();
});
app.MapGet("/health", () => Results.Ok(new { service = "AffixPrism Market Service", version = 1, source = "imported listings", liveCollection = false }));
app.MapGet("/api/leagues", () => Results.Ok(markets.Keys.Order().ToArray()));
var writeGate = new SemaphoreSlim(1, 1);
app.MapPost("/api/listings/import", async (HttpRequest request, CancellationToken token) =>
{
    try
    {
        using var reader = new StreamReader(request.Body);
        var json = await reader.ReadToEndAsync(token);
        var market = ComparableMarket.Parse(json, DateTimeOffset.UtcNow);
        string file = Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(market.League))) + ".json");
        await writeGate.WaitAsync(token);
        try { await File.WriteAllTextAsync(file + ".tmp", json, token); File.Move(file + ".tmp", file, true); markets[market.League] = market; }
        finally { writeGate.Release(); }
        return Results.Ok(new { league = market.League, imported = true });
    }
    catch (JsonException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (IOException) { return Results.Problem("Could not save the dataset."); }
});
app.MapPost("/api/price-check", (ComparableRequest request) =>
{
    if (request.Item?.Details == null || request.Filters == null || request.Filters.Count > 100 || string.IsNullOrWhiteSpace(request.League)) return Results.BadRequest(new { error = "Provide item details, league and at most 100 filters." });
    if (!markets.TryGetValue(request.League, out var market)) return Results.NotFound(new { error = "No listing dataset is loaded for this league. Import listings first; live collection is not connected." });
    try
    {
        var item = ItemParser.Parse(request.Item.Details) ?? throw new ArgumentException("Unsupported item text.");
        if (request.Filters.Any(x => x == null || string.IsNullOrWhiteSpace(x.Text))) throw new ArgumentException("Invalid filter text.");
        if (request.MaximumAgeDays is < 0 or > 30) throw new ArgumentException("Listing age must be 0–30 days.");
        var result = market.Search(request with { Item = item }, DateTimeOffset.UtcNow);
        return Results.Ok(result with { Rows = result.Rows.Take(100).ToArray() });
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});
app.Run();
