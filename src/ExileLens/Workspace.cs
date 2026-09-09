using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ExileLens.Core;

namespace ExileLens;
public partial class MainWindow
{
    private static readonly bool MarketIntegrationDeferred = false;
    private readonly MarketRepository localMarket = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExileLens", "market-service"));
    private readonly LiveTradeClient liveTrade = new(new HttpClient { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 16 * 1024 * 1024 });
    private bool useImportedListings;
    private readonly EconomyClient economy = new(new HttpClient { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 16 * 1024 * 1024 }, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExileLens", "economy"));
    private CancellationTokenSource? quoteRequest, guideRequest;
    private readonly List<RecentCheck> recent = new();
    private readonly List<SessionArea> areas = new();
    private readonly List<TrackedLoot> loot = new();
    private DateTimeOffset sessionStarted = DateTimeOffset.Now;
    private int sessionChecks;
    private bool selectingRecent;
    private int recentIndex;
    private void NavigateRecent(int direction)
    {
        int next=recentIndex+direction;
        if(next<0 || next>=recent.Count) return;
        recentIndex=next; selectingRecent=true;
        try { ShowItem(recent[next].Item); } finally { selectingRecent=false; }
        evaluation.SetHistoryNavigation(recentIndex+1<recent.Count,recentIndex>0);
        evaluation.SetQuote("Previously checked item · press Search for current prices");
    }
    private void ResetQuote()
    {
        quoteRequest?.Cancel(); quoteRequest?.Dispose(); quoteRequest = null;
        Quotes.ItemsSource = null;
        QuoteStatus.Text = "No market estimate loaded.";
        evaluation?.SetQuote(QuoteStatus.Text);
    }
    private void PopulateWorkspace(CopiedItem item)
    {
        FilterBase.Text = item.BaseType;
        FilterLevel.Clear(); FilterQuality.Clear(); FilterCorrupted.IsChecked = null;
        ComparisonRight.Text = item.Details;
        if (comparisonBaseline != null) ComparisonChanges.Text = ItemComparison.Describe(comparisonBaseline, item);
        if (!selectingRecent)
        {
            sessionChecks++;
            recent.Insert(0, new RecentCheck(item, DateTimeOffset.Now));
            recentIndex=0;
            if (recent.Count > 30) recent.RemoveAt(30);
            RecentItems.ItemsSource = recent.ToArray();
        }
        evaluation.SetHistoryNavigation(recentIndex+1<recent.Count,recentIndex>0);
        UpdateSession();
        SaveSession();
    }
    private async Task LookupEstimateAsync()
    {
        ResetQuote();
        if (currentItem == null) return;
        var item = currentItem;
        var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        quoteRequest = request;
        evaluation.SetQuote("Checking comparable listings…");
        try
        {
            if (smoke) { evaluation.SetQuote("Smoke test · no network request"); return; }
            ComparableResult? result = null;
            var criteria = Economy.UsesEquipmentListings(item) ? evaluation.BuildComparableRequest(settings.League) with { Currency = "Auto" } : null;
            if (useImportedListings && criteria != null) result = await localMarket.SearchAsync(criteria, request.Token);
            else if (criteria != null)
            {
                evaluation.SetQuote("Searching POE trade…");
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(request.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(25));
                result = await liveTrade.SearchAsync(criteria, deadline.Token, stage =>
                {
                    if (!request.IsCancellationRequested && quoteRequest == request) evaluation.SetQuote(stage);
                });

            }
            if (request.IsCancellationRequested || quoteRequest != request) return;
            if (result != null)
            {
                try
                {
                    var rates = await economy.CategoryAsync(new("Currency",true),settings.League,request.Token);
                    string target = evaluation.BuildComparableRequest(settings.League).Currency;
                    if (target == "Auto") target = result.Currency;
                    var conversions=PriceConversion.Rates(rates,target);
                    result=result with { Currency=target, ExchangeRates=conversions, RateNote=conversions.Count>0 ? $"Converted using {rates.Source} rates from {rates.FetchedAt.ToLocalTime():dd MMM HH:mm}" : "Exchange rates unavailable; only same-currency offers contribute" };
                }
                catch (HttpRequestException) { result=result with { RateNote="Exchange rates unavailable; only same-currency offers contribute" }; }
                if (request.IsCancellationRequested || quoteRequest != request) return;
                evaluation.SetComparableResult(result);
                ShowResaleCandidates(item,result);
                QuoteStatus.Text = $"{result.Source} · {result.Rows.Count} matches · {result.SellerCount} sellers";
                return;
            }
            if (Economy.Category(item) == null) { evaluation.SetQuote(ItemPresentation.IsGem(item) ? "No published market-price feed for this cut gem. Currency, uncut gems and lineage gems use exchange pricing." : "This item category is not supported by the current price sources."); return; }
            evaluation.SetQuote("Loading exchange market price…");
            var snapshot = await economy.LookupAsync(item, settings.League, request.Token);
            if (request.IsCancellationRequested || quoteRequest != request) return;
            var matches = Economy.Matching(snapshot.Rows, item);
            Quotes.ItemsSource = matches.ToArray();
            QuoteStatus.Text = matches.Count == 0 ? "No matching market estimate for this item in the selected league." : $"{snapshot.Source} · {settings.League} · fetched {snapshot.FetchedAt.ToLocalTime():dd MMM HH:mm}{(snapshot.Stale ? " · STALE" : "")} · category estimates";
            evaluation.SetQuote(QuoteStatus.Text, matches);
            if (Economy.Category(item)?.Exchange == true) evaluation.SetExchange(snapshot);
        }
        catch (OperationCanceledException) { if (!request.IsCancellationRequested && quoteRequest == request) evaluation.SetQuote("Trade request timed out. Try Check prices again."); }
        catch (Exception ex) when (ex is HttpRequestException or ArgumentException or NotSupportedException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException or KeyNotFoundException or InvalidOperationException)
        { if (!request.IsCancellationRequested && quoteRequest == request) { QuoteStatus.Text = ex.Message; evaluation.SetQuote(ex is HttpRequestException ? "Could not load prices. Check your connection and the data source. " + ex.Message : ex.Message); } }
    }
    private async void ImportComparableListings()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Import comparable listings", Filter = "Listing data (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(evaluation) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 10 * 1024 * 1024) throw new ArgumentException("Listing imports must be 10 MB or smaller.");
            var json = await File.ReadAllTextAsync(dialog.FileName, lifetime.Token);
            var market = await Task.Run(() => ComparableMarket.Parse(json, DateTimeOffset.UtcNow), lifetime.Token);
            if (market.League != settings.League) throw new ArgumentException($"This dataset is for {market.League}. Select that league in Settings before importing.");
            await localMarket.ImportAsync(json, lifetime.Token);
            useImportedListings = true;
            await LookupEstimateAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or HttpRequestException)
        { evaluation.SetQuote("Import failed: " + ex.Message); }
    }
    private async void RefreshEstimate(object sender, RoutedEventArgs e) => await LookupEstimateAsync();
    private void SelectRecent(object sender, SelectionChangedEventArgs e)
    {
        if (RecentItems.SelectedItem is not RecentCheck entry) return;
        recentIndex=recent.IndexOf(entry);
        selectingRecent = true;
        try { ShowItem(entry.Item); } finally { selectingRecent = false; }
        _ = LookupEstimateAsync();
    }
    private static int? ReadMinimum(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (int.TryParse(text, out int value) && value >= 0 && value <= 100) return value;
        throw new ArgumentException("Minimum item level and quality must be whole numbers from 0 to 100, or blank.");
    }
    private void PinComparison(object sender, RoutedEventArgs e)
    {
        if (currentItem == null) { Notice.Text = "Check an item before pinning a comparison."; return; }
        comparisonBaseline = currentItem;
        ComparisonLeft.Text = currentItem.Details;
        ComparisonChanges.Text = ItemComparison.Describe(currentItem, currentItem);
        SaveSession();
        Notice.Text = "Comparison pinned. Check another item, then open Compare.";
    }
    private void TrackLoot(object sender, RoutedEventArgs e)
    {
        if (currentItem == null) { Notice.Text = "Check an item before recording loot."; return; }
        var old = loot.FirstOrDefault(x => x.Name == currentItem.Name);
        if (old != null) loot.Remove(old);
        if (loot.Count >= 1000 && old == null) { Notice.Text = "Session loot limit reached. Reset session to start a new record."; return; }
        loot.Add(new TrackedLoot(currentItem.Name, (old?.Quantity ?? 0) + 1));
        LootRows.ItemsSource = loot.OrderBy(x => x.Name).ToArray();
        SaveSession();
        Notice.Text = "Recorded one item in this session's loot.";
        LootActionStatus.Text = $"Added {currentItem.Name} · session quantity {(old?.Quantity ?? 0) + 1}";
    }
    private void RecordArea(AreaEntry area)
    {
        areas.Insert(0, new SessionArea(area.DisplayName, area.Level, DateTimeOffset.Now));
        if (areas.Count > 200) areas.RemoveAt(200);
        AreaRows.ItemsSource = areas.ToArray();
        UpdateSession();
        SaveSession();
    }
    private void UpdateSession() => SessionStatus.Text = $"{(int)(previousSessionElapsed + (DateTimeOffset.Now - sessionStarted)).TotalMinutes} min · {sessionChecks} checks · {areas.Count} recent areas";
    private void ResetSession(object sender, RoutedEventArgs e)
    {
        previousSessionElapsed = TimeSpan.Zero; sessionCanSave = true;
        areas.Clear(); loot.Clear(); recent.Clear(); recentIndex=0; evaluation.SetHistoryNavigation(false,false); sessionChecks = 0; sessionStarted = DateTimeOffset.Now;
        AreaRows.ItemsSource = null; LootRows.ItemsSource = null; RecentItems.ItemsSource = null;
        UpdateSession();
        SaveSession();
    }
    private async Task LoadAutomaticGuideAsync(AreaEntry area, string league)
    {
        if (MarketIntegrationDeferred) return;
        try
        {
            var data = await economy.CategoryAsync(new("Expedition", true), league, lifetime.Token);
            if (currentArea == area && settings.League == league && !exiting) expedition.ShowMarket(data, league);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is HttpRequestException or ArgumentException or System.Text.Json.JsonException)
        { if (currentArea == area && settings.League == league && !exiting) expedition.MarketUnavailable(); }
    }
    private async void LoadGuide(object sender, RoutedEventArgs e)
    {
        if (MarketIntegrationDeferred) { GuideStatus.Text = "Market integration deferred. Your manual shortlist remains available."; return; }
        guideRequest?.Cancel(); guideRequest?.Dispose();
        var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); guideRequest = request;
        string category = ((ComboBoxItem)GuideKind.SelectedItem).Content.ToString()!;
        GuideRows.ItemsSource = null; GuideStatus.Text = "Loading reward prices…";
        try
        {
            if (smoke) { GuideStatus.Text = "Smoke test · no network request"; return; }
            var data = await economy.CategoryAsync(new(category, true), settings.League, request.Token);
            if (request.IsCancellationRequested || guideRequest != request) return;
            GuideRows.ItemsSource = data.Rows.OrderByDescending(x => x.Value).ToArray();
            GuideStatus.Text = $"{category} · {settings.League} · poe.ninja\nFetched {data.FetchedAt.ToLocalTime():dd MMM HH:mm}{(data.Stale ? " · STALE cache" : "")} · {data.Rows.Count} rewards";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is HttpRequestException or ArgumentException or System.Text.Json.JsonException)
        { if (!request.IsCancellationRequested) GuideStatus.Text = ex.Message; }
    }
}
