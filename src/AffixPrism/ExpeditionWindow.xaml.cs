using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using AffixPrism.Core;

namespace AffixPrism;
public partial class ExpeditionWindow : Window
{
    public event Action? Dismissed;
    public ExpeditionWindow() => InitializeComponent();
    public void Refresh(Settings settings, AreaEntry? area)
    {
        PriceSource.Text = "Manual reference prices";
        Opacity = settings.Opacity;
        ZoneText.Text = area?.DisplayName ?? "Expedition guide";
        MarketText.Text = $"{(string.IsNullOrWhiteSpace(settings.League) ? "League not set" : settings.League)} · {settings.Currency}";
        Rewards.ItemsSource = settings.Watchlist.OrderByDescending(x => x.Price).ThenBy(x => x.Name).ToList();
        EmptyText.Visibility = settings.Watchlist.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    public void ShowMarket(EconomySnapshot data, string league)
    {
        var rows = data.Rows.OrderByDescending(x => x.Value).Take(30).Select(x => new { x.Name, ValueLabel = x.Value.ToString("0.##"), DateLabel = x.Currency }).ToArray();
        Rewards.ItemsSource = rows;
        EmptyText.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        MarketText.Text = league + " · poe.ninja estimates";
        PriceSource.Text = $"Fetched {data.FetchedAt.ToLocalTime():dd MMM HH:mm}{(data.Stale ? " · STALE cache" : "")} · indicative values";
    }
    public void MarketUnavailable() => PriceSource.Text = "Market data unavailable · showing manual shortlist";
    private void DragPanel(object sender, DragDeltaEventArgs e) { Left += e.HorizontalChange; Top += e.VerticalChange; }
    private void HideGuide(object sender, RoutedEventArgs e) { Hide(); Dismissed?.Invoke(); }
}
