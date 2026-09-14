using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using AffixPrism.Core;
namespace AffixPrism;
public partial class MainWindow
{
    private Dictionary<string, EconomyRow> marketFavourites = new();
    private bool priceDescending = true;
    private string FavouriteKey(EconomyRow row) => settings.League + "|" + row.Name + "|" + row.Variant + "|" + row.Currency;
    private string FavouriteFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AffixPrism","market-favourites.json");
    private sealed record MarketViewRow(EconomyRow Item, bool Saved, EconomySnapshot? Rates = null, DateTimeOffset? Captured = null)
    {
        public string Name => Item.Name;
        public string Variant => Item.Variant;
        public string PriceLabel => MarketPriceDisplay.Format(Item,Rates,DateTimeOffset.UtcNow);
        public string? IconUrl => Item.IconUrl;
        public string CountLabel => Item.ListingCount?.ToString("N0") ?? "—";
        public string FavouriteLabel => Saved ? "★" : "☆";
        public string PriceDetails => MarketHoverDetails.Currencies(Item,Rates,DateTimeOffset.UtcNow);
        public string HistoryDetails => MarketHoverDetails.History(Item,Captured);
        public string TrendLabel => Item.ChangePercent?.ToString("+0.##;-0.##;0") + (Item.ChangePercent.HasValue ? "%" : "—");
        public string TrendColour => Item.ChangePercent is < 0 ? "#C34F45" : Item.ChangePercent is > 0 ? "#67AF58" : "#928B71";
        public System.Windows.Media.Geometry HistoryGeometry
        {
            get
            {
                var geometry=new System.Windows.Media.StreamGeometry();
                var values=Item.History.Where(x=>x.HasValue).Select(x=>(double)x!.Value).ToArray();
                if(values.Length<2) return geometry;
                double min=values.Min(),span=Math.Max(.001,values.Max()-min); bool drawing=false;
                using(var ctx=geometry.Open()) for(int i=0;i<Item.History.Count;i++)
                {
                    if(Item.History[i] is not { } value) { drawing=false; continue; }
                    var point=new Point(i*62d/Math.Max(1,Item.History.Count-1),22-((double)value-min)/span*18);
                    if(!drawing) ctx.BeginFigure(point,false,false); else ctx.LineTo(point,true,false);
                    drawing=true;
                }
                geometry.Freeze(); return geometry;
            }
        }
    }
    private void NavigateSettings(object sender, RoutedEventArgs e) => Tabs.SelectedItem = SettingsTab;
    private void NavigateSession(object sender, RoutedEventArgs e) => Tabs.SelectedItem = SessionTab;
    private void NavigatePrices(object sender, RoutedEventArgs e) => Tabs.SelectedItem = PricesTab;
    private void UpdateHome()
    {
        if (HomeElapsed == null) return;
        var elapsed = previousSessionElapsed + (DateTimeOffset.Now - sessionStarted);
        HomeElapsed.Text = $"{(int)elapsed.TotalHours} hr, {elapsed.Minutes} min, {elapsed.Seconds} sec";
        double visit = areas.Count > 0 ? Math.Clamp((DateTimeOffset.Now - areas[0].EnteredAt).TotalSeconds,0,elapsed.TotalSeconds) : 0;
        HomeVisitSegment.Width = new GridLength(Math.Max(.001,visit), GridUnitType.Star);
        HomeOtherSegment.Width = new GridLength(Math.Max(.001,elapsed.TotalSeconds-visit), GridUnitType.Star);
        HomeTimeLegend.Text = $"◆ Current area visit {TimeSpan.FromSeconds(visit):hh\\:mm\\:ss}     ◆ Other session time {TimeSpan.FromSeconds(Math.Max(0,elapsed.TotalSeconds-visit)):hh\\:mm\\:ss}";
        HomeCheckCount.Text = sessionChecks.ToString(); HomeAreaCount.Text = areas.Count.ToString(); HomeLootCount.Text = loot.Sum(x=>x.Quantity).ToString();
        HomeAreaRows.ItemsSource = areas.Take(5).ToArray(); HomeHistoryEmpty.Visibility = areas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HomeFavourites.ItemsSource = marketFavourites.Where(x=>x.Key.StartsWith(settings.League+"|",StringComparison.Ordinal)).Select(x=>x.Value.Name+"   "+x.Value.PriceLabel+" · saved reference").Take(6).ToArray();
        HomeFavouriteEmpty.Visibility = HomeFavourites.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void RestoreMarketFavourites()
    {
        if(smoke) return;
        try { if(File.Exists(FavouriteFile)) marketFavourites = JsonSerializer.Deserialize<Dictionary<string,EconomyRow>>(File.ReadAllText(FavouriteFile)) ?? new(); }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException) { MarketFeedStatus.Text="Saved market favourites could not be loaded."; }
    }
    private void ToggleMarketFavourite(object sender, RoutedEventArgs e)
    {
        if(sender is not Button { Tag: EconomyRow row }) return;
        string key=FavouriteKey(row);
        if(!marketFavourites.Remove(key)) marketFavourites[key]=row;
        if(!smoke) try { Directory.CreateDirectory(Path.GetDirectoryName(FavouriteFile)!); File.WriteAllText(FavouriteFile+".tmp",JsonSerializer.Serialize(marketFavourites)); File.Move(FavouriteFile+".tmp",FavouriteFile,true); }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { MarketFeedStatus.Text="Favourite changed for this session; could not save it to disk."; }
        FilterBrowserPrices(); UpdateHome(); RenderShortlist();
    }
    private void FilterFavourites(object sender,RoutedEventArgs e) => FilterBrowserPrices();
    private void SortMarketPrices(object sender,RoutedEventArgs e) { priceDescending=!priceDescending; FilterBrowserPrices(); }
}
