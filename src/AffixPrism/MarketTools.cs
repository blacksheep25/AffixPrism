using System;
using System.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using AffixPrism.Core;
namespace AffixPrism;
public partial class MainWindow
{
    private void ShowResaleCandidates(CopiedItem item, ComparableResult result)
    {
        var candidates = result.Rows.Select(row => (Row:row, Ask:SimilarItems.ConvertedPrice(row,result), Estimate:SimilarItems.Estimate(row.Item,result with { Rows = result.Rows.Where(r => !r.Account.Equals(row.Account,StringComparison.OrdinalIgnoreCase)).ToArray() })))
            .Where(x => x.Ask.HasValue && x.Estimate.Price > x.Ask)
            .OrderByDescending(x => x.Estimate.Price-x.Ask).Take(8).ToArray();
        ResaleCandidates.Text = $"{settings.League} · {result.CapturedAt.ToLocalTime():dd MMM HH:mm} · fetched sample only\n" +
            (candidates.Length == 0 ? "No supported resale gaps: at least three other similar sellers are required." :
            string.Join("\n\n", candidates.Select(x => $"{x.Row.Item.Name} · {x.Row.Account}\nAsk {x.Row.PriceLabel} → peer median {x.Estimate.Price:0.##} {result.Currency} · gross gap {x.Estimate.Price-x.Ask:0.##} {result.Currency}\n{x.Estimate.Sellers} other sellers · {x.Estimate.Similarity:P0} similarity")))
            + "\nAsking-price gaps are candidates for review, not guaranteed profit. Gold, costs and time to sell are excluded.";
    }
    private async void LoadOpportunities(object sender,RoutedEventArgs e)
    {
        var button=(Button)sender; button.IsEnabled=false;
        string league=settings.League;
        OpportunityRows.ItemsSource=null; OpportunityStatus.Text="Loading category prices…";
        try
        {
            string category=((ComboBoxItem)OpportunityCategory.SelectedItem).Tag.ToString()!;
            var data=await economy.CategoryAsync(new(category,false),league,lifetime.Token);
            if (settings.League!=league) { OpportunityStatus.Text="League changed. Load again."; return; }
            var gaps=data.Rows.Where(r=>r.Value>0 && r.Corrupted!=null).GroupBy(r=>(r.Name,r.BaseType,r.Currency,r.Variant)).Select(g=>new { Key=g.Key, Clean=g.Where(r=>r.Corrupted==false).OrderBy(r=>r.Value).FirstOrDefault(), Corrupt=g.Where(r=>r.Corrupted==true).OrderBy(r=>r.Value).FirstOrDefault() })
                .Where(g=>g.Clean!=null && g.Corrupt!=null && g.Corrupt.Value>g.Clean.Value).OrderByDescending(g=>g.Corrupt!.Value-g.Clean!.Value).Take(20)
                .Select(g=>$"{g.Key.Name} · {g.Key.Variant}\nUncorrupted {g.Clean!.PriceLabel} → corrupted {g.Corrupt!.PriceLabel}\nObserved premium {g.Corrupt.Value-g.Clean.Value:0.##} {g.Key.Currency} · outcome effects not specified").ToArray();
            OpportunityRows.ItemsSource=gaps;
            OpportunityStatus.Text=$"{league} · {data.Source} · {data.FetchedAt.ToLocalTime():dd MMM HH:mm}" + (data.Stale ? " · STALE" : "") + (gaps.Length==0 ? $"\nLoaded {data.Rows.Count} prices, but no usable uncorrupted/corrupted pairs. This feed cannot establish a corruption premium here. Browse category prices or enter researched amounts in the calculator." : $"\n{gaps.Length} paired variants ranked by observed premium.");
        }
        catch(Exception ex) { OpportunityStatus.Text=ex.Message; }
        finally { button.IsEnabled=true; }
    }
    private void CalculateOpportunity(object sender,RoutedEventArgs e)
    {
        decimal Read(TextBox box) => decimal.TryParse(box.Text,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var n) && n>=0 ? n : throw new ArgumentException("Enter non-negative numbers using a decimal point.");
        try
        {
            decimal buy=Read(PlanBuy),cost=Read(PlanCost),sale=Read(PlanSale),chance=Read(PlanChance)/100,failure=Read(PlanFailure);
            if(chance>1) throw new ArgumentException("Probability must be between 0 and 100%.");
            decimal expected=chance*sale+(1-chance)*failure-buy-cost;
            string currency=((ComboBoxItem)PlanCurrency.SelectedItem).Content.ToString()!;
            PlanResult.Text=$"Expected net: {expected:0.##} {currency} per attempt\nTarget outcome net: {sale-buy-cost:0.##} · other outcomes net: {failure-buy-cost:0.##}" + (sale>failure ? $"\nBreak-even target probability: {(buy+cost-failure)/(sale-failure):P1}" : "") + "\nUses your inputs. Gold cost and sale time are not included.";
        }
        catch(ArgumentException ex) { PlanResult.Text=ex.Message; }
    }
}
