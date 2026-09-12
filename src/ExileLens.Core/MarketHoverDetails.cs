namespace ExileLens.Core;
public static class MarketHoverDetails
{
    public static string Currencies(EconomyRow item, EconomySnapshot? rates, DateTimeOffset now)
    {
        string original=$"{item.Name}\nProvider price: {item.PriceLabel}";
        if(rates is not {Stale:false} || now < rates.FetchedAt || now-rates.FetchedAt>TimeSpan.FromHours(1)) return original+"\nFresh exchange rates unavailable.";
        string? basis=rates.Rows.FirstOrDefault()?.Currency;
        if(basis==null) return original+"\nExchange rates unavailable.";
        decimal? Rate(string name) => name==basis ? 1m : rates.Rows.FirstOrDefault(r=>r.Name==name && r.Currency==basis && r.Value>0)?.Value;
        if(Rate(item.Currency) is not { } source) return original+"\nNo conversion rate for this currency.";
        var lines=new List<string>{original,"","Exchange equivalents per item"};
        foreach(string currency in new[]{"Divine Orb","Exalted Orb","Chaos Orb"})
            if(Rate(currency) is { } rate) lines.Add($"{MarketPriceDisplay.Amount(item.Value*source/rate)} {currency}");
        lines.Add($"\nRates updated {rates.FetchedAt.ToLocalTime():dd MMM HH:mm}");
        lines.Add("Converted values; the feed does not supply seller asking currencies.");
        return string.Join("\n",lines);
    }
    public static string History(EconomyRow item, DateTimeOffset? captured)
    {
        var valid=item.History.Where(x=>x.HasValue).Select(x=>x!.Value).ToArray();
        if(valid.Length==0) return "No price-history samples supplied for this item.";
        string Signed(decimal n)=>n.ToString("+0.##;-0.##;0",System.Globalization.CultureInfo.InvariantCulture)+"%";
        return $"Provider trend · {item.History.Count} samples\n" +
            (captured.HasValue ? $"Updated {captured.Value.ToLocalTime():dd MMM HH:mm}\n" : "Saved reference history\n")+
            $"Overall change: {(item.ChangePercent.HasValue ? Signed(item.ChangePercent.Value) : "not supplied")}\nSample range: {Signed(valid.Min())} to {Signed(valid.Max())}\n\n"+
            string.Join("\n",item.History.Select((p,i)=>$"Sample {i+1}: {(p.HasValue ? Signed(p.Value) : "unavailable")}"))+
            "\n\nSamples run oldest to newest. Values are relative changes, not historical sale prices. Gaps represent missing data.";
    }
}
