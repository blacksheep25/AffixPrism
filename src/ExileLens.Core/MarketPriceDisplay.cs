using System.Globalization;
namespace ExileLens.Core;

public static class MarketPriceDisplay
{
    public static string Amount(decimal value) => value > 0 && value < .00000001m ? "<0.00000001" : value.ToString(value is > 0 and < .01m ? "0.########" : "0.##", CultureInfo.InvariantCulture);
    public static string Format(EconomyRow row, EconomySnapshot? rates, DateTimeOffset now)
    {
        if (row.Currency == "Divine Orb" && row.Value is > 0 and < 1 && rates is { Stale:false } && now >= rates.FetchedAt && now-rates.FetchedAt <= TimeSpan.FromHours(1))
        {
            var exalt = rates.Rows.FirstOrDefault(r=>r.Name=="Exalted Orb" && r.Currency==row.Currency && r.Value>0);
            if (exalt != null) return Amount(row.Value/exalt.Value)+" Exalted Orb";
        }
        return Amount(row.Value)+" "+row.Currency;
    }
}
