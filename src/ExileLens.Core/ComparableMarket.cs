using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ExileLens.Core;

public sealed record ListingPrice(decimal Amount, string Currency);
public sealed record ImportedListing(string Id, string Account, ListingPrice Price, string ItemText, DateTimeOffset ListedAt, bool Online, bool InstantBuy, string? IconUrl = null, int? Stock = null, IReadOnlyList<ItemSocket>? Sockets = null);
public sealed record ListingDocument(string League, string Source, DateTimeOffset CapturedAt, IReadOnlyList<ImportedListing> Listings);
public sealed record PriceConstraint(string Text, decimal? Minimum, decimal? Maximum, int ValueIndex = 0, string? Kind = null, int GroupId = 0);
public sealed record ComparableRequest(CopiedItem Item, string League, string Currency, IReadOnlyList<PriceConstraint> Filters, bool OnlineOnly, bool InstantBuyOnly, int? MaximumAgeDays, bool ExactBase = true);
public sealed record ComparableListing(ImportedListing Listing, CopiedItem Item, ItemAnalysis Analysis)
{
    public string PriceLabel => $"{Listing.Price.Amount:0.##} {Listing.Price.Currency}";
    public string StockLabel => Listing.Stock?.ToString() ?? "—";
    public string Account => Listing.Account;
    public string? IconUrl => ItemArtwork.SafeUrl(Listing.IconUrl);
    public int? ItemLevel => Analysis.ItemLevel;
    public int? Quality => Analysis.Quality;
    public string Listed => Listing.ListedAt.ToLocalTime().ToString("dd MMM");
}
public static class ItemArtwork
{
    public static string? SafeUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length != 0 || !uri.IsDefaultPort) return null;
        return uri.Host is "web.poecdn.com" or "cdn.poecdn.com" or "www.pathofexile.com" or "pathofexile.com" ? uri.AbsoluteUri : null;
    }
}
public sealed record ComparableResult(IReadOnlyList<ComparableListing> Rows, decimal? Median, decimal? LowerQuartile, decimal? UpperQuartile, int SellerCount, string Currency, string Source, DateTimeOffset CapturedAt, string? SearchUrl = null, int? TotalMatches = null, int? FetchedCount = null, IReadOnlyDictionary<string, decimal>? ExchangeRates = null, string? RateNote = null);

public sealed class ComparableMarket
{
    private readonly ListingDocument document;
    private readonly IReadOnlyList<ComparableListing> listings;
    private ComparableMarket(ListingDocument document, IReadOnlyList<ComparableListing> listings) { this.document = document; this.listings = listings; }
    public string League => document.League;
    public static ComparableMarket Parse(string json, DateTimeOffset now)
    {
        var doc = JsonSerializer.Deserialize<ListingDocument>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new JsonException("Empty listing document");
        if (string.IsNullOrWhiteSpace(doc.League) || string.IsNullOrWhiteSpace(doc.Source) || doc.Listings == null || doc.Listings.Count > 10000 || doc.CapturedAt > now.AddMinutes(5) || doc.CapturedAt < now.AddDays(-30)) throw new JsonException("Supply league, source, a capture time within 30 days and at most 10,000 listings.");
        var result = new List<ComparableListing>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var listing in doc.Listings)
        {
            if (listing == null || string.IsNullOrWhiteSpace(listing.Id) || string.IsNullOrWhiteSpace(listing.Account) || listing.Price == null || listing.Price.Amount <= 0 || string.IsNullOrWhiteSpace(listing.Price.Currency) || listing.ItemText == null || listing.ListedAt > doc.CapturedAt.AddMinutes(5) || listing.ListedAt > now.AddMinutes(5) || listing.ListedAt == default) throw new JsonException("Each listing needs an ID, seller, positive price, currency, item text and valid listing time.");
            var item = ItemParser.Parse(listing.ItemText) ?? throw new JsonException($"Listing {listing.Id} has unsupported item text.");
            item = item with { Sockets = listing.Sockets };
            if (!ids.Add(listing.Id)) throw new JsonException("Duplicate listing IDs are not allowed.");
            result.Add(new(listing, item, ItemAnalysis.From(item)));
        }
        return new(doc, result);
    }
    private static readonly Regex Numbers = new(@"(?<![\d.])[+-]?\d+(?:\.\d+)?", RegexOptions.CultureInvariant);
    public static string Signature(string text) => Numbers.Replace(ItemAnalysis.CleanTradeText(text), "#").Trim().ToLowerInvariant();
    private static decimal? NumericValue(string text, int index)
    {
        var matches = Numbers.Matches(text);
        var match = index >= 0 && index < matches.Count ? matches[index] : Match.Empty;
        return match.Success && decimal.TryParse(match.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) ? value : null;
    }
    public ComparableResult Search(ComparableRequest request, DateTimeOffset now)
    {
        if (!string.Equals(request.League, document.League, StringComparison.Ordinal)) throw new ArgumentException($"Imported listings belong to {document.League}. Select that league or import another dataset.");
        if (request.Currency == "Auto")
        {
            var groups = document.Listings.Select(l => l.Price.Currency).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(currency => Search(request with { Currency = currency }, now)).Where(r => r.Rows.Count > 0)
                .OrderByDescending(r => r.SellerCount).ThenBy(r => r.Currency,StringComparer.Ordinal).ToArray();
            if (groups.Length == 0) return new([],null,null,null,0,"Auto",document.Source,document.CapturedAt);
            var preferred=groups[0];
            return preferred with { Rows=groups.SelectMany(g=>g.Rows).ToArray(), Source=preferred.Source + " · automatic currency: " + preferred.Currency };
        }
        if (string.IsNullOrWhiteSpace(request.Currency)) throw new ArgumentException("Select a price currency.");
        if (document.CapturedAt < now.AddDays(-30)) throw new ArgumentException("Imported listings are more than 30 days old. Import fresher data.");
        if (request.Filters.Any(x => x.Minimum > x.Maximum)) throw new ArgumentException("A filter minimum exceeds its maximum.");
        if (request.MaximumAgeDays is < 0 or > 30 || request.Filters.Any(x => x.ValueIndex < 0 || x.ValueIndex > 100)) throw new ArgumentException("Invalid filter index or listing age.");
        var filterGroups = request.Filters.GroupBy(x => (Signature(x.Text), x.Kind, x.GroupId)).ToArray();
        var original = ItemAnalysis.From(request.Item);
        bool Matches(ComparableListing row)
        {
            if ((request.ExactBase ? !row.Item.BaseType.Equals(request.Item.BaseType, StringComparison.OrdinalIgnoreCase) : !row.Item.ItemClass.Equals(request.Item.ItemClass, StringComparison.OrdinalIgnoreCase)) || row.Item.Rarity != request.Item.Rarity || row.Analysis.CorruptionLevel != original.CorruptionLevel || row.Analysis.Unidentified != original.Unidentified) return false;
            if (request.Item.Rarity == "Unique" && !original.Unidentified && !row.Item.Name.Equals(request.Item.Name, StringComparison.OrdinalIgnoreCase)) return false;
            if (!row.Listing.Price.Currency.Equals(request.Currency, StringComparison.OrdinalIgnoreCase)) return false;
            if (request.OnlineOnly && !row.Listing.Online || request.InstantBuyOnly && !row.Listing.InstantBuy) return false;
            if (request.MaximumAgeDays is { } days && row.Listing.ListedAt < now.AddDays(-days)) return false;
            var owners = new Dictionary<int, int>();
            bool Assign(int groupIndex, HashSet<int> visited)
            {
                var group = filterGroups[groupIndex];
                for (int index = 0; index < row.Analysis.Lines.Count; index++)
                {
                    var line = row.Analysis.Lines[index];
                    if (Signature(line.Text) != group.Key.Item1 || group.Key.Kind != null && line.Kind != group.Key.Kind) continue;
                    if (!group.All(filter => (!filter.Minimum.HasValue && !filter.Maximum.HasValue) || NumericValue(line.Text, filter.ValueIndex) is { } value && (!filter.Minimum.HasValue || value >= filter.Minimum) && (!filter.Maximum.HasValue || value <= filter.Maximum))) continue;
                    if (!visited.Add(index)) continue;
                    if (!owners.TryGetValue(index, out int owner) || Assign(owner, visited)) { owners[index] = groupIndex; return true; }
                }
                return false;
            }
            for (int index = 0; index < filterGroups.Length; index++) if (!Assign(index, new HashSet<int>())) return false;
            return true;
        }
        var rows = listings.Where(Matches).OrderBy(x => x.Listing.Price.Amount).ToArray();
        var values = rows.GroupBy(x => x.Account.Trim(), StringComparer.OrdinalIgnoreCase).Select(x => x.Min(y => y.Listing.Price.Amount)).Order().ToArray();
        decimal Quantile(decimal p)
        {
            decimal index = (values.Length - 1) * p; int lo = (int)decimal.Floor(index), hi = (int)decimal.Ceiling(index);
            return values[lo] + (values[hi] - values[lo]) * (index - lo);
        }
        // Fewer than three independent sellers is insufficient for an estimate.
        return new(rows, values.Length >= 3 ? Quantile(.5m) : null, values.Length >= 3 ? Quantile(.25m) : null, values.Length >= 3 ? Quantile(.75m) : null, values.Length, request.Currency, document.Source, document.CapturedAt);
    }
}
