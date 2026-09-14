using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AffixPrism.Core;

public sealed class EconomyClient(HttpClient http, string cacheDirectory)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset blockedUntil;
    private readonly Dictionary<string, DateTimeOffset> attempts = new();
    private sealed record Cache(string Json, DateTimeOffset FetchedAt, string? ETag);
    public async Task<EconomySnapshot> LookupAsync(CopiedItem item, string league, CancellationToken token)
    {
        var category = Economy.Category(item) ?? throw new NotSupportedException("Automatic pricing is not available for this item category. Rare and magic items need a modifier-aware pricing source.");
        return await CategoryAsync(category, league, token);
    }
    public async Task<EconomySnapshot> CategoryAsync(EconomyCategory category, string league, CancellationToken token, TimeSpan? maximumAge = null)
    {
        if (string.IsNullOrWhiteSpace(league)) throw new ArgumentException("Select your league in Settings to load market prices.");
        string path = $"/poe2/api/economy/{(category.Exchange ? "exchange" : "stash")}/current/{(category.Exchange ? "" : "item/")}overview?league={Uri.EscapeDataString(league)}&type={Uri.EscapeDataString(category.Type)}";
        var response = await GetAsync(path, token, maximumAge);
        string? currency = Economy.PrimaryCurrency(response.Cache.Json);
        if (currency == null && !category.Exchange)
        {
            var core = await GetAsync($"/poe2/api/economy/exchange/current/overview?league={Uri.EscapeDataString(league)}&type=Currency", token);
            currency = Economy.PrimaryCurrency(core.Cache.Json);
        }
        return new(Economy.Parse(response.Cache.Json, category.Exchange, currency).Select(r=>r with {CategoryType=category.Type,ExchangeCategory=category.Exchange}).ToArray(), response.Cache.FetchedAt, response.Cached, response.Stale);
    }
    private async Task<(Cache Cache, bool Cached, bool Stale)> GetAsync(string path, CancellationToken token, TimeSpan? maximumAge = null)
    {
        await gate.WaitAsync(token);
        try
        {
            string file = Path.Combine(cacheDirectory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path))) + ".json");
            Cache? cached = null;
            try { if (File.Exists(file)) cached = JsonSerializer.Deserialize<Cache>(await File.ReadAllTextAsync(file, token)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
            var now = DateTimeOffset.UtcNow;
            if (cached != null && now >= cached.FetchedAt && now - cached.FetchedAt < (maximumAge ?? TimeSpan.FromHours(1))) return (cached, true, false);
            if (now < blockedUntil || (attempts.TryGetValue(path, out var attempted) && now - attempted < TimeSpan.FromMinutes(5)))
            {
                if (cached != null) return (cached, true, true);
                throw new HttpRequestException("Market data is cooling down after a recent request. Please try again in a few minutes.");
            }
            attempts[path] = now;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://poe.ninja" + path);
                request.Headers.UserAgent.ParseAdd("AffixPrism/0.2 (personal-development)");
                if (cached?.ETag is { } tag) request.Headers.TryAddWithoutValidation("If-None-Match", tag);
                using var response = await http.SendAsync(request, token);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    blockedUntil = response.Headers.RetryAfter?.Date ?? now + (response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(5));
                    if (blockedUntil < now + TimeSpan.FromMinutes(5)) blockedUntil = now + TimeSpan.FromMinutes(5);
                }
                if (response.StatusCode == HttpStatusCode.NotModified && cached != null)
                {
                    cached = cached with { FetchedAt = now };
                    await SaveAsync(file, cached, token);
                    return (cached, true, false);
                }
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync(token);
                using (var parsed = JsonDocument.Parse(json))
                    if (!parsed.RootElement.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array) throw new JsonException("Unexpected market response");
                var fresh = new Cache(json, now, response.Headers.ETag?.ToString());
                await SaveAsync(file, fresh, token);
                return (fresh, false, false);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException || (ex is OperationCanceledException && !token.IsCancellationRequested))
            {
                if (cached != null) return (cached, true, true);
                if (ex is HttpRequestException network && network.InnerException is System.Net.Sockets.SocketException socket && socket.SocketErrorCode == System.Net.Sockets.SocketError.AccessDenied)
                    throw new HttpRequestException("Windows blocked the market connection to poe.ninja. Allow AffixPrism network access, then refresh prices.", ex);
                throw new HttpRequestException("Market data is unavailable. Check your connection, then refresh prices.", ex);
            }
        }
        finally { gate.Release(); }
    }
    private static async Task SaveAsync(string file, Cache cache, CancellationToken token)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(file)!); await File.WriteAllTextAsync(file, JsonSerializer.Serialize(cache), token); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
