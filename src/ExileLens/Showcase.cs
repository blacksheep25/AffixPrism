using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ExileLens.Core;

namespace ExileLens;
public partial class MainWindow
{
    // Render real controls with isolated, synthetic listings. Never save demo data to user storage.
    private void CaptureShowcase()
    {
        Directory.CreateDirectory("artifacts/showcase");
        Width = 1440; Height = 1000; Opacity = 1;
        UpdateHome(); Tabs.SelectedItem = HomeTab;
        Capture("artifacts/showcase/reference-home.png");
        Tabs.SelectedItem = ItemTab;
        Capture("artifacts/showcase/main-item-check.png");
        Tabs.SelectedItem = SessionTab;
        Capture("artifacts/showcase/main-session.png");
        var lootRight=SessionLootPanel.TransformToAncestor(this).Transform(new System.Windows.Point(SessionLootPanel.ActualWidth,0)).X;
        var areaLeft=SessionAreaPanel.TransformToAncestor(this).Transform(new System.Windows.Point()).X;
        if(areaLeft<lootRight) throw new Exception("Session panels overlap");
        Tabs.SelectedItem = MarketTab;
        Capture("artifacts/showcase/main-market.png");
        Tabs.SelectedItem = PricesTab;
        browserRows = new[] { new EconomyRow("Demonstration currency", "", "Fixture only", 2m, "Exalted Orb", null, null) { ChangePercent=12,History=new decimal?[]{null,0,2,-1,5,8,12} }, new EconomyRow("Example rune", "", "Fixture only", 12m, "Exalted Orb", null, null) { ChangePercent=-8,History=new decimal?[]{0,1,-2,null,-4,-6,-8} } };
        MarketFeedStatus.Text = "DEMO DATA · illustrative prices for layout verification";
        FilterBrowserPrices();
        PriceSearch.Text = "rune";
        if (MarketPriceRows.Items.Count != 1) throw new Exception("Market name search failed");
        PriceSearch.Clear();
        var demoFavourite = browserRows[0];
        ToggleMarketFavourite(new System.Windows.Controls.Button { Tag=demoFavourite },new System.Windows.RoutedEventArgs());
        OnlyFavourites.IsChecked=true; FilterBrowserPrices();
        if(MarketPriceRows.Items.Count!=1) throw new Exception("Market favourite filter failed");
        OnlyFavourites.IsChecked=false; FilterBrowserPrices();
        RenderShortlist(); Tabs.SelectedItem=ExpeditionTab; Capture("artifacts/showcase/shortlist.png");
        if(ShortlistRows.Items.Count!=1) throw new Exception("Saved favourite missing from Shortlist");
        Tabs.SelectedItem=RuneTab; Capture("artifacts/showcase/rune-page.png");
        if(RuneHost.Content==null) throw new Exception("Rune Helper has no embedded controls");
        Tabs.SelectedItem=GuidesTab; Capture("artifacts/showcase/encounters.png");
        Tabs.SelectedItem=PricesTab;
        Capture("artifacts/showcase/market-prices.png");
        if (Topmost || ComparisonTab.Visibility != System.Windows.Visibility.Collapsed) throw new Exception("Main window remains topmost or exposes duplicate Compare navigation");
        Tabs.SelectedItem = ItemTab;
        var view = new EvaluationWindow(true) { Resources = Resources, Width = 800, Height = 1040 };
        var item = ItemParser.Parse(File.ReadAllText("tests/Fixtures/quarterstaff.txt"))!;
        view.SetItem(item, "DEMO · illustrative prices");
        view.VerifyBoundsVisibility();
        view.VerifyFilterPicker();
        view.VerifyHiddenMods();
        if (!view.ItemDescriptionCard.IsAncestorOf(view.ItemRows) || view.ItemDescriptionCard.IsAncestorOf(view.ActiveFilters))
            throw new Exception("Item descriptions and filter controls must have separate containers");
        view.Show();
        var time = DateTimeOffset.UtcNow;
        var data = new ListingDocument("Demo", "DEMO DATA", time,
            Enumerable.Range(1, 5).Select(i => new ImportedListing("demo-"+i, "Demo seller "+i,
                new ListingPrice(18+i, "Divine Orb"), item.Details.Replace("Maelström Song", "Demonstration Staff").Replace("123(120-139)%", "125(120-139)%"), time, true, true)).ToArray());
        var market = ComparableMarket.Parse(JsonSerializer.Serialize(data), time);
        view.SetComparableResult(market.Search(new ComparableRequest(item,"Demo","Divine Orb",Array.Empty<PriceConstraint>(),true,true,null),time));
        view.VerifyListingComparison(window => Capture("artifacts/showcase/comparison.png",window));
        view.ResizeToItemContent(); view.UpdateLayout();
        Capture("artifacts/showcase/price-check.png",view);
        view.SetItem(ItemParser.Parse("Item Class: Quest Items\nRarity: Quest\nOrigin Spark\n--------\nA burgeoning emergent\nflare of empowered life\n--------\nCan be combined with the Origin Cradle within the Tower of Origins"),"Demo");
        view.Height=440; view.UpdateLayout();
        Capture("artifacts/showcase/quest-item.png",view);
        if(view.CheckPricesButton.Content?.ToString()!="Open quest item wiki" || view.EstimatePanel.Visibility!=System.Windows.Visibility.Collapsed || view.EquipmentFilters.Visibility!=System.Windows.Visibility.Collapsed) throw new Exception("Quest view retained pricing controls");
        view.SetItem(item,"Demo");
        if(view.EstimatePanel.Visibility!=System.Windows.Visibility.Visible || view.EquipmentFilters.Visibility!=System.Windows.Visibility.Visible) throw new Exception("Equipment controls did not return after quest inspection");
        view.Close();
    }
}
