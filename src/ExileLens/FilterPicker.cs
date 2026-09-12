using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExileLens.Core;

namespace ExileLens;
public partial class EvaluationWindow
{
    public void VerifyFilterPicker()
    {
        FilterSearch.Text="skills";
        var choices=FilterChoices.Children.OfType<CheckBox>().ToArray();
        if(choices.Length==0 || choices.Any(c=>!((TextBlock)c.Content).Text.Contains("skills",StringComparison.OrdinalIgnoreCase))) throw new Exception("Filter search failed");
        var first=choices[0]; first.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
        if(!drafts.Any(d=>d.Filter.Enabled && d.Filter.Text.Contains("skills",StringComparison.OrdinalIgnoreCase))) throw new Exception("Filter picker did not select stat");
        ResetDefaultFilters(this,new RoutedEventArgs()); FilterSearch.Clear();
    }
    private void FindFilters(object sender, TextChangedEventArgs e) => RefreshFilterChoices();
    private void RefreshFilterChoices()
    {
        if (FilterChoices == null || FilterSearch == null) return;
        FilterChoices.Children.Clear();
        var suggested = item == null ? Array.Empty<string>() : DefaultItemFilters.Suggested(item).ToArray();
        string query = FilterSearch.Text.Trim();
        var groups = drafts.GroupBy(d => d.Filter.GroupId)
            .Where(g => g.First().Filter.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(g => suggested.Contains(g.First().Filter.Text))
            .ThenByDescending(g => g.First().Filter.Enabled).ToArray();
        foreach (var group in groups)
        {
            var first = group.First().Filter;
            bool recommended = suggested.Contains(first.Text);
            var choice = new CheckBox {
                IsChecked = first.Enabled, Margin = new Thickness(0,4,0,4),
                Content = new TextBlock { Text = (recommended ? "★ " : "") + first.Text,
                    TextWrapping = TextWrapping.Wrap, FontSize = 11,
                    Foreground = new SolidColorBrush(recommended ? Color.FromRgb(220,199,143) : Color.FromRgb(190,186,170)) },
                ToolTip = recommended ? "Suggested for this item type. Tick to match this stat; adjust its bounds below." : "Tick to match this stat. Adjust its bounds below."
            };
            int id = group.Key;
            choice.Click += (_, _) => { if (rowToggles.TryGetValue(id, out var toggle)) toggle(); };
            FilterChoices.Children.Add(choice);
        }
        if (groups.Length == 0) FilterChoices.Children.Add(new TextBlock { Text = "No matching numeric stats.", Foreground = Foreground, TextWrapping = TextWrapping.Wrap });
    }
}
