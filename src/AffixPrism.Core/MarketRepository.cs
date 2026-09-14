using System.Security.Cryptography;
using System.Text;
namespace AffixPrism.Core;

public sealed class MarketRepository(string directory)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, (DateTime Stamp, long Size, ComparableMarket Market)> cache = new(StringComparer.Ordinal);
    private string FileFor(string league) => Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(league))) + ".json");
    public async Task ImportAsync(string json, CancellationToken token)
    {
        if (Encoding.UTF8.GetByteCount(json) > 10 * 1024 * 1024) throw new ArgumentException("Listing imports must be 10 MB or smaller.");
        var market = await Task.Run(() => ComparableMarket.Parse(json, DateTimeOffset.UtcNow), token);
        await gate.WaitAsync(token);
        try
        {
            Directory.CreateDirectory(directory);
            string file = FileFor(market.League), temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { await File.WriteAllTextAsync(temporary, json, token); File.Move(temporary, file, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            cache.Remove(market.League);
        }
        finally { gate.Release(); }
    }
    public async Task<ComparableResult?> SearchAsync(ComparableRequest request, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.League)) throw new ArgumentException("Choose and save your league in Settings.");
        await gate.WaitAsync(token);
        try
        {
            var file = new FileInfo(FileFor(request.League));
            if (!file.Exists) return null;
            if (file.Length > 10 * 1024 * 1024) throw new ArgumentException("The stored listing dataset is too large.");
            if (!cache.TryGetValue(request.League, out var entry) || entry.Stamp != file.LastWriteTimeUtc || entry.Size != file.Length)
            {
                var json = await File.ReadAllTextAsync(file.FullName, token);
                var market = await Task.Run(() => ComparableMarket.Parse(json, DateTimeOffset.UtcNow), token);
                entry = (file.LastWriteTimeUtc, file.Length, market); cache[request.League] = entry;
            }
            token.ThrowIfCancellationRequested();
            return await Task.Run(() => entry.Market.Search(request, DateTimeOffset.UtcNow), token);
        }
        finally { gate.Release(); }
    }
}
