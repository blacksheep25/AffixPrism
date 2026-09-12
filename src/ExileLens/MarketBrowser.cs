using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ExileLens.Core;

namespace ExileLens;
public partial class MainWindow
{
    private readonly DispatcherTimer marketRefresh = new() { Interval = TimeSpan.FromMinutes(5) };
    private IReadOnlyList<EconomyRow> browserRows = Array.Empty<EconomyRow>();
    private int browserVersion;
    private bool browserReady;
    private EconomySnapshot? browserRates;
    private DateTimeOffset? browserCaptured;
    private void InitializeMarketBrowser()
    {
        foreach (var entry in new[] { ("Currency", "Currency", true), ("Runes", "Runes", true), ("Omens", "Ritual", true), ("Essences", "Essences", true), ("Soul cores", "SoulCores", true), ("Uncut gems", "UncutGems", true), ("Lineage gems", "LineageSupportGems", true), ("Fragments", "Fragments", true), ("Unique weapons", "UniqueWeapons", false), ("Unique armour", "UniqueArmours", false), ("Unique accessories", "UniqueAccessories", false) })
            PriceCategory.Items.Add(new ListBoxItem { Content = entry.Item1, Tag = new EconomyCategory(entry.Item2, entry.Item3) });
        PriceCategory.SelectedIndex = 0;
        browserReady = true; RestoreMarketFavourites();
        marketRefresh.Tick += async (_, _) => { if (IsVisible && Tabs.SelectedItem == PricesTab) await LoadBrowserPrices(); };
        IsVisibleChanged += async (_, _) => { if (IsVisible && Tabs.SelectedItem == PricesTab && !smoke) await LoadBrowserPrices(); };
        Closed += (_, _) => { marketRefresh.Stop(); browserVersion++; };
        if (!smoke) marketRefresh.Start();
        SizeChanged += (_, _) => AdaptWorkspace();
        Loaded += (_, _) => { AdaptWorkspace(); if (!smoke) Tabs.SelectedItem = HomeTab; UpdateHome(); };
    }
    private void AdaptWorkspace()
    {
        Resources["MarketHistoryWidth"] = new GridLength(ActualWidth < 1150 ? 0 : 135);
        Resources["MarketHistoryVisibility"] = ActualWidth < 1150 ? Visibility.Collapsed : Visibility.Visible;
        bool compact = ActualWidth < 920;
        ItemWorkspaceGrid.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        var actions = (FrameworkElement)ItemWorkspaceGrid.Children[1];
        Grid.SetColumn(actions, compact ? 0 : 1); Grid.SetRow(actions, compact ? 1 : 0);
        actions.Margin = compact ? new Thickness(0,16,0,0) : new Thickness(0);
    }
    private async void PriceCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (browserReady && !smoke) await LoadBrowserPrices();
    }
    private void PriceSearchChanged(object sender, TextChangedEventArgs e) { if (browserReady) FilterBrowserPrices(); }
    private async void RefreshMarketClick(object sender, RoutedEventArgs e) => await LoadBrowserPrices();
    private void FilterBrowserPrices()
    {
        MarketPriceSort.Content = priceDescending ? "Price ↓ Highest" : "Price ↑ Lowest";
        MarketPriceSort.ToolTip = priceDescending ? "Highest price first within each currency. Click for lowest first." : "Lowest price first within each currency. Click for highest first.";
        string search = PriceSearch.Text.Trim();
        var source = OnlyFavourites.IsChecked == true ? marketFavourites.Where(x=>x.Key.StartsWith(settings.League+"|",StringComparison.Ordinal)).Select(x=>x.Value) : browserRows;
        var matching = source.Where(r => r.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || r.Variant.Contains(search, StringComparison.OrdinalIgnoreCase));
        var ordered = priceDescending ? matching.OrderBy(r=>r.Currency).ThenByDescending(r=>r.Value) : matching.OrderBy(r=>r.Currency).ThenBy(r=>r.Value);
        MarketPriceRows.ItemsSource = ordered.Select(r=>new MarketViewRow(r,marketFavourites.ContainsKey(FavouriteKey(r)),browserRates,OnlyFavourites.IsChecked==true ? null : browserCaptured)).ToArray();
        MarketEmptyState.Visibility = MarketPriceRows.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        MarketEmptyState.Text = OnlyFavourites.IsChecked == true ? "No saved items match your search. Use ☆ on a price row to save it." : search.Length > 0 ? "No items match your search." : "No market prices available. Refresh to retry.";
    }
    private async Task LoadBrowserPrices()
    {
        if (!browserReady || smoke || PriceCategory.SelectedItem is not ListBoxItem { Tag: EconomyCategory category }) return;
        int request = ++browserVersion;
        string league = settings.League;
        browserRates = null;
        MarketCategoryTitle.Text = ((ListBoxItem)PriceCategory.SelectedItem).Content.ToString();
        browserRows = Array.Empty<EconomyRow>(); FilterBrowserPrices();
        MarketFeedStatus.Text = $"{league} · loading market prices…";
        RefreshMarketButton.IsEnabled = false;
        try
        {
            var snapshot = await economy.CategoryAsync(category, league, lifetime.Token, TimeSpan.FromMinutes(5));
            if (request != browserVersion || league != settings.League) return;
            EconomySnapshot? rates = null;
            if(!snapshot.Stale)
            {
                if(category.Type=="Currency" && category.Exchange) rates=snapshot;
                else try { rates=await economy.CategoryAsync(new("Currency",true),league,lifetime.Token,TimeSpan.FromMinutes(5)); }
                catch(System.Net.Http.HttpRequestException) { /* Original currency remains available without rates. */ }
            }
            if (request != browserVersion || league != settings.League) return;
            browserRates=rates;
            browserCaptured=snapshot.FetchedAt;
            browserRows = snapshot.Rows.Where(r => r.Value > 0).ToArray(); FilterBrowserPrices();
            MarketFeedStatus.Text = $"{league} · {snapshot.Source} · {snapshot.FetchedAt.ToLocalTime():dd MMM HH:mm} · {browserRows.Count} prices" +
                (snapshot.Stale ? " · STALE — connection unavailable; showing last saved prices" : " · refreshes every 5 minutes while visible") +
                (browserRows.Count == 0 ? "\nNo prices supplied for this category in this league." : "");
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or System.IO.IOException or OperationCanceledException or ArgumentException or System.Text.Json.JsonException)
        {
            if (request == browserVersion) MarketFeedStatus.Text = $"{league} · {ex.Message}";
        }
        finally { if (request == browserVersion) RefreshMarketButton.IsEnabled = true; }
    }
}
