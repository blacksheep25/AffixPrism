using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AffixPrism.Core;
namespace AffixPrism;
public partial class EvaluationWindow
{
    private bool CompactCurrency => item != null && Economy.Category(item)?.Type == "Currency";
    private void ConfigureCurrencyCard()
    {
        CurrencyQuote.Visibility = CompactCurrency ? Visibility.Visible : Visibility.Collapsed;
        ItemRows.Visibility = CompactCurrency ? Visibility.Collapsed : Visibility.Visible;
        CurrencyStock.Visibility = CompactCurrency ? Visibility.Visible : Visibility.Collapsed;
        ItemHeaderArt.MaxHeight = CompactCurrency ? 0 : double.PositiveInfinity;
        ItemHeaderArt.Margin = CompactCurrency ? new Thickness(0) : new Thickness(0,6,0,6);
        ((FrameworkElement)ItemFrame.Parent).Height = CompactCurrency ? 42 : double.NaN;
        ItemSocketsView.Visibility = CompactCurrency ? Visibility.Collapsed : Visibility.Visible;
        if (CompactCurrency)
        {
            ItemFrame.Height = 38;
            ItemFrame.ToolTip = item!.Details;
            CurrencyStock.Text = $"Stock: {CurrencyStack():N0}";
            HiddenModsButton.Visibility = Visibility.Collapsed;
            EstimatePanel.Visibility = Visibility.Collapsed;
            CheckPricesButton.Content = "Search";
            MinHeight = 240;
            Width = 480;
        }
        else MinHeight = 400;
    }
    private decimal CurrencyStack()
    {
        var match = System.Text.RegularExpressions.Regex.Match(item!.Details, @"Stack Size: ([\d,]+)");
        return match.Success && decimal.TryParse(match.Groups[1].Value.Replace(",", ""), out var n) ? n : 1;
    }
    private void CurrencyStatus(string status)
    {
        CurrencyQuote.Children.Clear();
        CurrencyQuote.Children.Add(new TextBlock { Text = status, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Foreground = Theme.Brush("Text"), Margin = new Thickness(8) });
    }
    private void RenderCurrencyQuote(EconomySnapshot snapshot, EconomySnapshot? rates, EconomyRow row)
    {
        CurrencyQuote.Children.Clear();
        var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(8,12,8,6) };
        decimal left = row.Value < 1 ? 1 / row.Value : 1;
        decimal right = row.Value < 1 ? 1 : row.Value;
        panel.Children.Add(new TextBlock { Text = $"≈ {MarketPriceDisplay.Amount(left)}  ⇄  {MarketPriceDisplay.Amount(right)}", FontSize = 20, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var icon = rates?.Rows.FirstOrDefault(r => r.Name.Equals(row.Currency, StringComparison.OrdinalIgnoreCase))?.IconUrl;
        if (icon != null) panel.Children.Add(new RemoteItemIcon { Url = icon, Width = 26, Height = 30, Margin = new Thickness(6,0,0,0), ToolTip = row.Currency });
        panel.Children.Add(new TextBlock { Text = row.Currency, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6,0,12,0) });
        var history = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var graph = new Canvas { Width = 72, Height = 24 };
        var values = row.History.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        Brush trend = row.ChangePercent < 0 ? Brushes.Tomato : Brushes.YellowGreen;
        if (values.Length > 1)
        {
            decimal min = values.Min(), span = values.Max() - min;
            var line = new Polyline { Stroke = trend, StrokeThickness = 1.5 };
            for(int i=0;i<row.History.Count;i++)
            {
                if (row.History[i] is not {} v) { if(line.Points.Count>0)graph.Children.Add(line); line=new Polyline { Stroke=trend,StrokeThickness=1.5 }; continue; }
                line.Points.Add(new Point(i*72.0/(row.History.Count-1),span==0 ? 12 : 22-(double)((v-min)/span)*20));
            }
            graph.Children.Add(line);
            history.Children.Add(graph);
        }
        history.Children.Add(new TextBlock { Text = row.ChangePercent is {} change ? $"{change:+0.0;-0.0;0}% · last 7 days" : "History unavailable", Foreground = row.ChangePercent.HasValue ? trend : Theme.Brush("Muted"), FontSize = 10 });
        panel.Children.Add(history);
        CurrencyQuote.Children.Add(panel);
        CurrencyQuote.Children.Add(new TextBlock { Text = $"{snapshot.Source} · {snapshot.FetchedAt.ToLocalTime():dd MMM HH:mm}" + (snapshot.Stale ? " · STALE" : ""), FontSize = 10, Foreground = Theme.Brush("Muted"), TextAlignment = TextAlignment.Center, Margin = new Thickness(0,0,0,8) });
        CurrencyQuote.ToolTip = $"{MarketPriceDisplay.Amount(left)} {item!.Name} ≈ {MarketPriceDisplay.Amount(right)} {row.Currency}\nEach: {row.PriceLabel}\nYour stock: {MarketPriceDisplay.FormatQuantity(row,CurrencyStack(),snapshot.Stale ? null : rates,DateTimeOffset.UtcNow)}\nIndicative market ratio, not an executable offer.\n" + string.Join(" → ", row.History.Select(v=>v?.ToString("0.##") ?? "missing"));
        QueueContentResize();
    }
}
