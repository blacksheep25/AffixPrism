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
        var view = new EvaluationWindow(true) { Resources = Resources, Width = 800, Height = 1040 };
        var item = ItemParser.Parse(File.ReadAllText("tests/Fixtures/quarterstaff.txt"))!;
        view.SetItem(item, "DEMO · illustrative prices");
        view.Show();
        var time = DateTimeOffset.UtcNow;
        var data = new ListingDocument("Demo", "DEMO DATA", time,
            Enumerable.Range(1, 5).Select(i => new ImportedListing("demo-"+i, "Demo seller "+i,
                new ListingPrice(18+i, "Divine Orb"), item.Details.Replace("Maelström Song", "Demonstration Staff").Replace("123(120-139)%", "125(120-139)%"), time, true, true)).ToArray());
        var market = ComparableMarket.Parse(JsonSerializer.Serialize(data), time);
        view.SetComparableResult(market.Search(new ComparableRequest(item,"Demo","Divine Orb",Array.Empty<PriceConstraint>(),true,true,null),time));
        view.VerifyListingComparison(window => Capture("artifacts/showcase/comparison.png",window));
        Capture("artifacts/showcase/price-check.png",view);
        view.Close();
    }
}
