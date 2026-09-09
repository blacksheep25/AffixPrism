namespace ExileLens.Core;

public sealed record PriceDiagnostics(int Shown,int SimilarOffers,int SimilarSellers,int MissingRates,int? Fetched,int? Found)
{
    public bool PartialSample => Found.HasValue && Fetched.HasValue && Fetched.Value<Found.Value;
    public string Summary => $"{SimilarSellers} similar independent sellers from {Shown} shown offers" +
        (MissingRates>0 ? $" · {MissingRates} offers excluded from valuation: missing exchange rates" : "") +
        (PartialSample ? $" · sample: {Fetched} of {Found:N0} trade matches fetched" : "");
    public static PriceDiagnostics From(CopiedItem item,ComparableResult result)
    {
        var priced=result.Rows.Where(r=>SimilarItems.ConvertedPrice(r,result).HasValue).ToArray();
        var peers=priced.Where(r=>SimilarItems.Score(item,r.Item)>=.8m).ToArray();
        return new(result.Rows.Count,peers.Length,peers.Select(r=>r.Account.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            result.Rows.Count-priced.Length,result.FetchedCount,result.TotalMatches);
    }
}
