namespace AffixPrism.Core;
public static class PriceConversion
{
    public static IReadOnlyDictionary<string,decimal> Rates(EconomySnapshot snapshot,string target)
    {
        if(snapshot.Stale || DateTimeOffset.UtcNow - snapshot.FetchedAt > TimeSpan.FromHours(2)) return new Dictionary<string,decimal>();
        var rows=snapshot.Rows.Where(r=>r.Value>0).ToArray();
        string? primary=rows.FirstOrDefault()?.Currency;
        var values=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
        if(primary==null) return values;
        values[primary]=1;
        foreach(var group in rows.Where(r=>r.Currency==primary).GroupBy(r=>r.Name,StringComparer.OrdinalIgnoreCase))
            if(group.Select(r=>r.Value).Distinct().Count()==1) values[group.Key]=group.First().Value;
        if(!values.TryGetValue(target,out var divisor)) return new Dictionary<string,decimal>();
        return values.ToDictionary(p=>p.Key,p=>p.Value/divisor,StringComparer.OrdinalIgnoreCase);
    }
}
