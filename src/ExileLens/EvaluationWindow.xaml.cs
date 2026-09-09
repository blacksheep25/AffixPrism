using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ExileLens.Core;
namespace ExileLens;
public partial class EvaluationWindow : Window
{
    private OutsideClickDismiss? outsideClick;
    public event Action? Dismissed;
    private CopiedItem? item;
    private bool exactBase = true;
    private string? currentQueryUrl;
    private ListingComparisonWindow? comparison;
    private bool building;
    private readonly Dictionary<int, Action> rowToggles = new();
    private readonly List<Expander> valueExpanders = new();
    public event Action? FiltersChanged;
    private readonly List<(EvaluationFilter Filter, TextBox Min, TextBox Max)> drafts = new();
    private readonly Dictionary<string, string> savedDrafts = new();
    public bool IsTestMode { get; set; }
    private string DraftKey => item?.Details ?? "";
    private readonly TextBox level = new(), quality = new(), maxLevel = new(), maxQuality = new();
    private readonly CheckBox corrupted = new() { IsThreeState = true };
    public event Action<TradeFilters>? SearchRequested;
    public event Action? SettingsRequested;
    public event Action? RefreshRequested;
    public event Action? ImportRequested;
    public event Action? PinRequested;
    private sealed record ViewPreferences(int Currency, int Status, int Age, bool Broad, bool ListingsExpanded = true);
    private static string PreferencesPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExileLens", "evaluation-ui.json");
    public EvaluationWindow(bool smoke = false)
    {
        IsTestMode = smoke;
        InitializeComponent();
        InitializeQol();
        building = true; LoadPreferences(); if (!smoke) ResultCurrency.SelectedIndex = 3; building = false;
        ResultCurrency.SelectionChanged += (_, _) => InvalidatePrices();
        SellerMode.SelectionChanged += (_, _) => InvalidatePrices();
        ListingAge.SelectionChanged += (_, _) => InvalidatePrices();
        var menu = new ContextMenu { Style = (Style)FindResource(typeof(ContextMenu)), ItemContainerStyle = (Style)FindResource(typeof(MenuItem)) };
        var pin = new MenuItem { Header = "Pin for comparison" }; pin.Click += (_, _) => { PinRequested?.Invoke(); ResultStatus.Text = "Item pinned · open Compare from Settings"; };
        var reset = new MenuItem { Header = "Reset default filters" }; reset.Click += ResetDefaultFilters;
        var crafting = new MenuItem { Header = "Search as crafting base" }; crafting.Click += CraftingBaseFilters;
        var wiki = new MenuItem { Header = "Open item wiki" };
        wiki.Click += (_,_) => { if(item != null) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.poe2wiki.net/wiki/" + Uri.EscapeDataString(item.Rarity == "Unique" ? item.Name : item.BaseType)) { UseShellExecute = true }); };
        menu.Items.Add(pin); menu.Items.Add(reset); menu.Items.Add(crafting); menu.Items.Add(wiki); ItemFrame.ContextMenu = menu;
        ItemFrame.ToolTip = "Right-click: reset filters, open wiki, comparison actions";
        ListingsPanel.Expanded += (_, _) => { if (!building) { SavePreferences(); QueueContentResize(); } };
        ListingsPanel.Collapsed += (_, _) => { if (!building) { SavePreferences(); QueueContentResize(); } };
        Closing += (_, _) => { SavePreferences(); foreach (var window in pinnedWindows.ToArray()) window.Close(); bookmarkWindow?.Close(); };
        IsVisibleChanged += (_, _) => {
            outsideClick?.Dispose(); outsideClick=null;
            if (!IsVisible) { StoreDraft(); SavePreferences(); comparison?.Close(); comparison = null; Dismissed?.Invoke(); }
            else if(!IsTestMode) outsideClick=new OutsideClickDismiss(this);
        };
        Closed += (_,_) => { outsideClick?.Dispose(); outsideClick=null; };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && e.OriginalSource is TextBox box) { SaveDraft(this, new RoutedEventArgs()); e.Handled = true; } else if (e.Key == System.Windows.Input.Key.Escape) { SavePreferences(); Hide(); e.Handled = true; } };
        Loaded += (_, _) => { if (!IsTestMode) { FitToScreen(false); QueueContentResize(); } };
        SizeChanged += (_, e) => { if (e.WidthChanged) QueueContentResize(); };
        DpiChanged += (_, _) => { if (!IsTestMode) Dispatcher.BeginInvoke(new Action(() => { FitToScreen(false); ResizeToItemContent(); })); };
        ItemHeaderArt.IsVisibleChanged += (_, _) => QueueContentResize();
    }
    public void SetItem(CopiedItem? value, string league)
    {
        StoreDraft();
        comparison?.Close(); comparison = null;
        QolStatus.Text="";
        building = true;
        rowToggles.Clear();
        valueExpanders.Clear();
        item = value; ItemSocketsView.Item = value; exactBase = true;
        UpdateBaseFilter();
        BookmarkButton.Content = "☆ Bookmark";
        BookmarkButton.IsEnabled = PinScreenButton.IsEnabled = value != null;
        ItemHeaderArt.Url = null;
        Caption.Text = "Exile Lens · Evaluate" + (string.IsNullOrWhiteSpace(league) ? " · choose league" : " · " + league);
        drafts.Clear();
        ItemRows.Children.Clear(); SideRows.Children.Clear();
        // Detach reusable controls before rebuilding their rows.
        foreach (var control in new FrameworkElement[] { level, quality, maxLevel, maxQuality, corrupted })
            if (control.Parent is Panel panel) panel.Children.Remove(control);
        ItemTitle.Text = value?.Name ?? "No item captured";
        ItemBase.Text = value != null && Economy.UsesEquipmentListings(value) ? value.BaseType : "";
        level.Clear(); quality.Clear(); maxLevel.Clear(); maxQuality.Clear(); corrupted.IsChecked = null;
        SetQuote("Check an item or import comparable listings");
        ResultStatus.Text = "Select filters, then check prices";
        DesecratedHeader.Visibility = Visibility.Collapsed;
        if (value == null) { building = false; ResetFilterUndo(); return; }
        bool exchange = Economy.UsesExchange(value);
        bool equipment = Economy.UsesEquipmentListings(value);
        EquipmentFilters.Visibility = equipment ? Visibility.Visible : Visibility.Collapsed;
        FilterColumn.Width = new GridLength(equipment ? 184 : 0);
        PricePresets.Visibility = SearchPresetBar.Visibility = equipment ? Visibility.Visible : Visibility.Collapsed;
        ListingsPanel.Visibility = equipment ? Visibility.Visible : Visibility.Collapsed;
        CheckPricesButton.Content = exchange ? "Refresh exchange price" : equipment ? "Search" : "Refresh market price";
        var analysis = ItemAnalysis.From(value);

        Brush rarity = new SolidColorBrush(value.Rarity switch {
            "Rare" => Color.FromRgb(233, 221, 135), "Magic" => Color.FromRgb(145, 151, 235),
            "Unique" => Color.FromRgb(209, 131, 61), _ => Color.FromRgb(205, 200, 183)
        });
        bool gem = ItemPresentation.IsGem(value);
        if (gem) rarity = new SolidColorBrush(Color.FromRgb(108,201,195));
        DesecratedHeader.Visibility = analysis.Desecrated ? Visibility.Visible : Visibility.Collapsed;
        ItemTitle.Foreground = rarity; ItemBase.Foreground = rarity; ItemFrame.BorderBrush = rarity;
        ItemTitle.Text = value.Name.ToUpperInvariant(); ItemBase.Text = gem ? (value.ItemClass.Contains("Support",StringComparison.OrdinalIgnoreCase) || value.Details.Contains("Support,") ? "Support" : "Skill Gem") : ItemBase.Text.ToUpperInvariant();
        BodyScroll.ScrollToTop();
        string? previousKind = null;
        int groupId = 0;
        foreach (var line in analysis.Lines)
        {
            int currentGroup = groupId++;
            var row = new Grid { Margin = new Thickness(0, previousKind != null && line.Kind != previousKind ? 7 : 1, 0, 1) };
            previousKind = line.Kind;
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            bool property = line.Kind == "Property";
            var badge = new TextBlock { Text = line.RollLabel.Length > 0 ? line.RollLabel : line.TierLabel, Foreground = new SolidColorBrush(Color.FromRgb(207, 139, 231)), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Position within copied roll range; not a pricing score.\n" + line.Tooltip };
            row.Children.Add(badge);
            var label = new TextBlock { Text = (line.Kind == "Pseudo" ? "Σ " : "") + ItemTextStyle.Display(line), FontStyle = line.Kind is "Flavour" or "Gem description" or "Instructions" ? FontStyles.Italic : FontStyles.Normal, FontFamily = new FontFamily("Georgia"), FontSize = 13, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 0, 3), Foreground = ItemTextStyle.Brush(line), ToolTip = ItemMetadata.Style(line).Name + "\n" + line.Tooltip };
            System.Windows.Documents.Typography.SetCapitals(label, gem && line.Kind is "Flavour" or "Gem description" or "Instructions" ? FontCapitals.Normal : FontCapitals.SmallCaps);
            if (gem && line.Kind is "Flavour" or "Gem description" or "Instructions") { Grid.SetColumnSpan(label, 1); label.FontSize = 15; }
            Grid.SetColumn(label, 1); row.Children.Add(label);
            int numberCount = System.Text.RegularExpressions.Regex.Matches(line.Text, @"(?<![\d.])[+-]?\d+(?:\.\d+)?").Count;
            if (equipment && numberCount > 0 && line.Kind is not ("Flavour" or "Gem description" or "Instructions" or "Description" or "Gem tags"))
            {
                var group = new List<(EvaluationFilter Filter, TextBox Min, TextBox Max)>();
                var fieldStack = new StackPanel { Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
                var extraFields = new StackPanel();
                for (int index = 0; index < numberCount; index++)
                {
                    var filter = new EvaluationFilter(line, index, currentGroup); filter.Preset(BroadPreset.IsChecked == true);
                    TextBox minimum = new() { Text = filter.Minimum }, maximum = new();
                    var fields = RangeFields(minimum, maximum); fields.Margin = new Thickness(0, index > 0 ? 2 : 0, 0, 0);
                    string valueName = ValueName(line.Text, index, numberCount);
                    minimum.ToolTip = $"Minimum {valueName}"; maximum.ToolTip = $"Maximum {valueName}";
                    if (index == 0) fieldStack.Children.Add(fields);
                    else
                    {
                        extraFields.Children.Add(new TextBlock { Text = valueName, Foreground = new SolidColorBrush(Color.FromRgb(185,176,154)), FontSize = 10, Margin = new Thickness(0,4,0,1) });
                        extraFields.Children.Add(fields);
                    }
                    group.Add((filter, minimum, maximum)); drafts.Add((filter, minimum, maximum));
                    minimum.TextChanged += (_, _) => { filter.Minimum = minimum.Text; InvalidatePrices(); };
                    maximum.TextChanged += (_, _) => { filter.Maximum = maximum.Text; InvalidatePrices(); };
                }
                var sideGroup = new StackPanel { Margin = new Thickness(0,0,0,8) };
                sideGroup.Children.Add(new TextBlock { Text = line.Text, TextWrapping = TextWrapping.Wrap, Foreground = ItemTextStyle.Brush(line), FontSize = 10, Margin = new Thickness(4,3,4,3) });
                sideGroup.Children.Add(fieldStack); SideRows.Children.Add(sideGroup);
                if (numberCount > 1)
                {
                    var expander = new Expander { Header = line.Text.Contains("Damage", StringComparison.OrdinalIgnoreCase) ? "Damage bounds" : line.Text.StartsWith("Requires:") ? "Requirements" : "Other bounds", Content = extraFields, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(185,176,154)), ToolTip = "This line has multiple numbers. The first fields constrain its first number; expand to constrain the remaining numbers separately." };
                    fieldStack.Children.Add(expander); valueExpanders.Add(expander);
                    expander.Expanded += (_, _) => QueueContentResize(); expander.Collapsed += (_, _) => QueueContentResize();
                }
                void Toggle()
                {
                    bool enabled = !group[0].Filter.Enabled;
                    foreach (var member in group) member.Filter.Enabled = enabled;
                    label.Background = enabled ? new SolidColorBrush(Color.FromRgb(56, 56, 48)) : Brushes.Transparent;

                    InvalidatePrices();
                }
                label.Cursor = Cursors.Hand; label.ToolTip = line.Tooltip + "\nClick to include/exclude this filter. Each field pair follows a numeric value from left to right.";
                label.MouseLeftButtonUp += (_, _) => Toggle(); label.Tag = (Action)Toggle; rowToggles[currentGroup] = Toggle;

                foreach (var member in group)
                {
                    member.Min.GotKeyboardFocus += (_, _) => { if (!group[0].Filter.Enabled) Toggle(); };
                    member.Max.GotKeyboardFocus += (_, _) => { if (!group[0].Filter.Enabled) Toggle(); };
                }
            }
            ItemRows.Children.Add(row);
        }
        if (!savedDrafts.ContainsKey(DraftKey))
        {
            var defaults = DefaultItemFilters.Select(value);
            foreach (var group in drafts.Where(x => defaults.Contains(x.Filter.Text)).GroupBy(x => x.Filter.GroupId))
                if (rowToggles.TryGetValue(group.Key, out var toggle)) toggle();
        }
        RestoreDraft();
        building = false;
        ResetFilterUndo();
        if (!savedDrafts.ContainsKey(DraftKey) && profiles.FirstOrDefault(p=>p.Name==activeProfile && p.ItemClass.Equals(value.ItemClass,StringComparison.OrdinalIgnoreCase)) is { } profile)
        { ApplyProfile(profile); ResetFilterUndo(); }
        QueueContentResize();
    }
    private static Panel RangeFields(TextBox min, TextBox max)
    {
        var panel = new Grid();
        panel.ColumnDefinitions.Add(new ColumnDefinition()); panel.ColumnDefinitions.Add(new ColumnDefinition());
        min.Padding = max.Padding = new Thickness(6, 2, 6, 2);
        min.FontSize = max.FontSize = 13;
        min.Tag = "min"; max.Tag = "max";
        min.ToolTip = "Local filter minimum"; max.ToolTip = "Local filter maximum";
        min.Margin = new Thickness(0, 0, 3, 0); min.MaxLength = max.MaxLength = 18;
        min.MinHeight = max.MinHeight = 28;
        foreach (var field in new[] { min, max })
        {
            field.GotKeyboardFocus += (_, _) => field.SelectAll();
            field.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (!field.IsKeyboardFocusWithin) { Window.GetWindow(field)?.Activate(); field.Focus(); e.Handled = true; }
            };
        }
        System.Windows.Automation.AutomationProperties.SetName(min, "Minimum");
        System.Windows.Automation.AutomationProperties.SetName(max, "Maximum");
        panel.Children.Add(min); Grid.SetColumn(max, 1); panel.Children.Add(max);
        return panel;
    }
    private static string ValueName(string text, int index, int count)
    {
        if (count == 2 && text.Contains("Damage", StringComparison.OrdinalIgnoreCase)) return index == 0 ? "lower damage roll" : "upper damage roll";
        var numbers = System.Text.RegularExpressions.Regex.Matches(text, @"(?<![\d.])[+-]?\d+(?:\.\d+)?");
        return $"number {index + 1} (copied: {numbers[index].Value})";
    }
    private void CompareListing(object sender, RoutedEventArgs e)
    {
        if (item == null || sender is not FrameworkElement { DataContext: ComparableListing listing }) return;
        comparison?.Close();
        comparison = new ListingComparisonWindow(item, listing, ItemHeaderArt.Url) { Owner = this, Resources = Resources, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        comparison.Show();
    }
    public void VerifyListingComparison(Action<Window>? capture = null)
    {
        if (Variants.ItemsSource is not IEnumerable<ComparableListing> listings) throw new Exception("No listings to compare");
        var listing = listings.First();
        ListingsPanel.IsExpanded = true; UpdateLayout();
        static Button? FindRow(DependencyObject parent)
        {
            if (parent is Button { Name: "ListingCompareButton" } button) return button;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                if (FindRow(VisualTreeHelper.GetChild(parent, i)) is { } row) return row;
            return null;
        }
        var rowButton = FindRow(Variants) ?? throw new Exception("Listing comparison action is missing");
        rowButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (comparison == null || comparison.Yours != item!.Details || comparison.Seller != listing.Item.Details || !comparison.Changes.Contains(listing.Item.Name)) throw new Exception("Seller comparison did not preserve both items");
        capture?.Invoke(comparison);
        comparison.Close(); comparison = null;
        ListingsPanel.IsExpanded = false;
    }

    public void SetQuote(string status, System.Collections.Generic.IReadOnlyList<EconomyRow>? rows = null)
    {
        currentQueryUrl=null; OpenQueryButton.Visibility=Visibility.Collapsed;
        averageListing = null; AverageCompare.Visibility = Visibility.Collapsed; ExchangePanel.Visibility = Visibility.Collapsed;
        Estimate.Text = rows is { Count: > 0 } ? rows[0].PriceLabel : "—";
        EstimateSource.Text = status;
        ResultStatus.Text = "";
        EmptyResults.Text = rows is { Count: > 0 } ? "Category estimate shown above · no individual seller offers" : "";
        ResultCount.Text = "0 / 0";
        Variants.ItemsSource = null;
    }
    public void SetExchange(EconomySnapshot snapshot)
    {
        if (item == null) return;
        var matches = Economy.Matching(snapshot.Rows,item).Where(r => r.Value > 0).ToArray();
        if (matches.Length == 0) { ExchangePanel.Visibility = Visibility.Collapsed; return; }
        ExchangePanel.Header = "Exchange market price";
        ExchangePanel.Visibility = Visibility.Visible;
        var quantity = System.Text.RegularExpressions.Regex.Match(item.Details, @"Stack Size: ([\d,]+)");
        decimal stack = quantity.Success && decimal.TryParse(quantity.Groups[1].Value.Replace(",",""),out var n) ? n : 1;
        ExchangeInfo.Text = string.Join("\n\n", matches.Select(r => $"1 {r.Name} ≈ {r.Value:0.####} {r.Currency}\n1 {r.Currency} ≈ {1/r.Value:0.####} {r.Name}\nYour stack ({stack:0}): ≈ {stack*r.Value:0.##} {r.Currency}"))
            + $"\n\n{snapshot.Source} · {snapshot.FetchedAt.ToLocalTime():dd MMM HH:mm}" + (snapshot.Stale ? " · STALE" : "")
            + "\nIndicative ratios, not executable buy/sell orders. Confirm quantities and gold cost in the in-game exchange.";
        QueueContentResize();
    }
    private sealed record DraftRow(string Text, bool Enabled, string Minimum, string Maximum, int ValueIndex, int GroupId);
    private void StoreDraft()
    {
        if (item == null || drafts.Count == 0) return;
        savedDrafts[DraftKey] = JsonSerializer.Serialize(drafts.Select(x => new DraftRow(x.Filter.Text, x.Filter.Enabled, x.Min.Text, x.Max.Text, x.Filter.ValueIndex, x.Filter.GroupId)).ToArray());
        if (savedDrafts.Count > 30) savedDrafts.Remove(savedDrafts.Keys.First());
    }
    private void RestoreDraft()
    {
        if (!savedDrafts.TryGetValue(DraftKey, out var json)) return;
        var rows = JsonSerializer.Deserialize<DraftRow[]>(json)!;
        for (int i = 0; i < Math.Min(rows.Length, drafts.Count); i++)
        {
            var row = rows[i]; var draft = drafts[i];
            if (row.Text != draft.Filter.Text || row.ValueIndex != draft.Filter.ValueIndex || row.GroupId != draft.Filter.GroupId) continue;
            draft.Min.Text = row.Minimum; draft.Max.Text = row.Maximum;
            if (row.Enabled && !draft.Filter.Enabled)
            {
                if (rowToggles.TryGetValue(draft.Filter.GroupId, out var toggle)) toggle();
            }
        }
    }
    private void InvalidatePrices()
    {
        if (building || item == null) return;
        RecordFilterChange();
        FiltersChanged?.Invoke();
        SetQuote("Filters changed · press Search");
        ResultStatus.Text = "Displayed prices cleared because search criteria changed";
    }
    private void ApplyPreset(object sender, RoutedEventArgs e)
    {
        building=true;
        foreach (var row in drafts) { row.Filter.Preset(BroadPreset.IsChecked == true); row.Min.Text = row.Filter.Minimum; row.Max.Text = row.Filter.Maximum; }
        building=false; InvalidatePrices();
        ResultStatus.Text = "Filter values updated · press Search";
    }
    private void SaveDraft(object sender, RoutedEventArgs e)
    {
        foreach (var row in drafts)
            if (row.Filter.Validate() is { } error) { ResultStatus.Text = error; row.Min.Focus(); return; }
        StoreDraft();
        SavePreferences();
        ResultStatus.Text = $"{drafts.Count(x => x.Filter.Enabled)} filters selected";
        RefreshRequested?.Invoke();
    }
    private void ImportListings(object sender, RoutedEventArgs e) => ImportRequested?.Invoke();
    public void SetPreferredCurrency(string currency)
    {
        if (!IsTestMode) currency = "Auto";
        var option = ResultCurrency.Items.OfType<ComboBoxItem>().FirstOrDefault(x => string.Equals(x.Content?.ToString(), currency, StringComparison.OrdinalIgnoreCase));
        if (option == null) { option = new ComboBoxItem { Content = currency }; ResultCurrency.Items.Add(option); }
        ResultCurrency.SelectedItem = option;
    }
    public ComparableRequest BuildComparableRequest(string league)
    {
        if (item == null) throw new ArgumentException("Check an item first.");
        var filters = new List<PriceConstraint>();
        foreach (var row in drafts.Where(x => x.Filter.Enabled))
        {
            if (row.Filter.Validate() is { } error) throw new ArgumentException(error);
            filters.Add(new(row.Filter.Text, string.IsNullOrWhiteSpace(row.Min.Text) ? null : decimal.Parse(row.Min.Text, CultureInfo.InvariantCulture), string.IsNullOrWhiteSpace(row.Max.Text) ? null : decimal.Parse(row.Max.Text, CultureInfo.InvariantCulture), row.Filter.ValueIndex, row.Filter.Kind, row.Filter.GroupId));
        }
        string currency = ((ComboBoxItem)ResultCurrency.SelectedItem).Content.ToString()!;
        int? age = ListingAge.SelectedIndex switch { 1 => 1, 2 => 3, 3 => 7, _ => null };
        return new(item, league, currency, filters, SellerMode.SelectedIndex == 1, SellerMode.SelectedIndex == 0, age, exactBase);
    }
    private ComparableListing? averageListing;
    private void CompareAverage(object sender, RoutedEventArgs e)
    {
        if (item == null || averageListing == null) return;
        new ListingComparisonWindow(item, averageListing, ItemHeaderArt.Url) { Owner = this, Resources = Resources, WindowStartupLocation = WindowStartupLocation.CenterScreen }.Show();
    }
    public void SetComparableResult(ComparableResult result)
    {
        SocketStrip.Observe(result.Rows.SelectMany(r=>r.Item.Sockets ?? Array.Empty<ItemSocket>()));
        currentQueryUrl=result.SearchUrl; OpenQueryButton.Visibility=currentQueryUrl==null ? Visibility.Collapsed : Visibility.Visible;
        ItemHeaderArt.Url = result.Rows.Select(x => x.IconUrl).FirstOrDefault(x => x != null);
        QueueContentResize();
        Estimate.Text = "—";
        EstimateSource.Text = item != null && ItemAnalysis.From(item).Unidentified ? "Unidentified base-item listings · hidden identity and modifiers cannot be valued reliably." : "No reliable item estimate · not enough sufficiently similar listings. Use Match modifiers to narrow the search.";
        var similar = item == null ? null : SimilarItems.Estimate(item, result);
        if(similar!=null && !similar.Price.HasValue) EstimateSource.Text=similar.Explanation;
        averageListing = null; AverageCompare.Visibility = Visibility.Collapsed;
        if (similar?.Price is { } recommended && similar.Average is { } average)
        {
            Estimate.Text = $"Suggested: {recommended:0.##} {result.Currency}";
            EstimateSource.Text = $"{similar.Sellers} similar sellers · {similar.Similarity:P0} mean similarity · asking-price median\n{result.RateNote}";
            averageListing = new(new ImportedListing("average", "Similar-item average (synthetic)", new(recommended,result.Currency), average.Details,result.CapturedAt,false,false,ItemHeaderArt.Url),average,ItemAnalysis.From(average));
            AverageCompare.Visibility = Visibility.Visible;
        }
        ResultStatus.Text = (similar?.Explanation ?? "") + "\n" + $"{result.Source} · captured {result.CapturedAt.ToLocalTime():dd MMM HH:mm}\n{result.Rows.Count} matching listings · asking prices, not completed sales\n{(result.Source.Contains("BROADER RESULTS") ? "Broader base-item comparison · your selected filters found no listings" : drafts.Any(x => x.Filter.Enabled) ? drafts.Count(x => x.Filter.Enabled) + " selected numeric filters" : "Base and rarity only · select modifiers to narrow results")}";
        ResultCount.Text = result.TotalMatches is { } total ? $"{result.Rows.Count} shown · {result.FetchedCount ?? 0} fetched · {total:N0} found on trade" : $"{result.Rows.Count} shown";
        if(item!=null) { var coverage=PriceDiagnostics.From(item,result); ResultCount.ToolTip=coverage.Summary; ResultStatus.Text+="\n"+coverage.Summary; }
        Variants.ItemsSource = result.Rows.OrderByDescending(r => item == null ? 0 : SimilarItems.Score(item,r.Item)).Take(100).ToArray();

        EmptyResults.Text = result.Rows.Count == 0 ? result.TotalMatches > 0 ? "Trade found offers, but none of the fetched sample passed the local filters. Open Trade query to inspect all results." : "No offers found for this search." : "";
    }
    private void OpenTradeQuery(object sender, RoutedEventArgs e)
    {
        if (currentQueryUrl != null && Uri.TryCreate(currentQueryUrl,UriKind.Absolute,out var uri) && uri.Scheme=="https" && uri.Host=="www.pathofexile.com")
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute=true });
    }
    private void ToggleBaseFilter(object sender, MouseButtonEventArgs e)
    {
        if (item == null || !Economy.UsesEquipmentListings(item)) return;
        exactBase = !exactBase; UpdateBaseFilter(); InvalidatePrices(); e.Handled = true;
    }
    private void UpdateBaseFilter()
    {
        ItemBase.Background = exactBase ? new SolidColorBrush(Color.FromRgb(56,56,48)) : Brushes.Transparent;
        ItemBase.ToolTip = exactBase ? "Exact base filter enabled · click to include other bases, then Search" : "Exact base filter disabled · click to match this base, then Search";
        ItemBase.Cursor = Cursors.Hand;
    }
    private void MatchModifiers(object sender, RoutedEventArgs e)
    {
        building = true;
        foreach (var group in drafts.Where(x => x.Filter.Kind is not ("Property" or "Rune" or "Enchant" or "Implicit")).GroupBy(x => x.Filter.GroupId))
            if (!group.First().Filter.Enabled && rowToggles.TryGetValue(group.Key, out var toggle)) toggle();
        building = false;
        SaveDraft(sender,e);
    }
    private void ResetDefaultFilters(object sender, RoutedEventArgs e)
    {
        if(item == null) return;
        building = true;
        var defaults = DefaultItemFilters.Select(item);
        foreach(var group in drafts.GroupBy(x => x.Filter.GroupId))
        {
            bool wanted = defaults.Contains(group.First().Filter.Text);
            if(group.First().Filter.Enabled != wanted && rowToggles.TryGetValue(group.Key,out var toggle)) toggle();
            foreach(var member in group)
            {
                member.Filter.Preset(BroadPreset.IsChecked == true);
                member.Min.Text = member.Filter.Minimum; member.Max.Text = member.Filter.Maximum;
            }
        }
        exactBase = true; UpdateBaseFilter(); building = false; StoreDraft(); InvalidatePrices();
    }
    private void CraftingBaseFilters(object sender, RoutedEventArgs e)
    {
        if(item == null || !Economy.UsesEquipmentListings(item)) return;
        building = true;
        foreach(var group in drafts.GroupBy(x => x.Filter.GroupId))
        {
            var first = group.First();
            bool wanted = first.Filter.Kind == "Property" && first.Filter.Text.StartsWith("Item Level:");
            if(first.Filter.Enabled != wanted && rowToggles.TryGetValue(group.Key,out var toggle)) toggle();
            if(wanted) foreach(var member in group)
            {
                member.Filter.Preset(false); member.Min.Text = member.Filter.Minimum; member.Max.Text = "";
            }
        }
        exactBase = true; UpdateBaseFilter(); building = false;
        StoreDraft(); InvalidatePrices();
        ResultStatus.Text = "Crafting-base preset: exact base and item level; existing rarity and corruption restrictions still apply. Press Search.";
    }
    private void RefreshPrices(object sender, RoutedEventArgs e) => RefreshRequested?.Invoke();

    private static int? Minimum(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (int.TryParse(text, out int value) && value >= 0 && value <= 100) return value;
        throw new ArgumentException("Enter a whole-number minimum from 0 to 100, or leave it blank.");
    }
    private void Search(object sender, RoutedEventArgs e)
    {
        if (item == null) return;
        try { SearchRequested?.Invoke(new(item.BaseType, true, Minimum(level.Text), Minimum(quality.Text), corrupted.IsChecked, Minimum(maxLevel.Text), Minimum(maxQuality.Text))); }
        catch (ArgumentException ex) { ResultStatus.Text = ex.Message; }
    }
    private void SettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();
    private void Dismiss(object sender, RoutedEventArgs e) { SavePreferences(); Hide(); }
    private void LoadPreferences()
    {
        if (IsTestMode) return;
        try
        {
            if (!File.Exists(PreferencesPath)) return;
            var prefs = JsonSerializer.Deserialize<ViewPreferences>(File.ReadAllText(PreferencesPath));
            if (prefs == null) return;
            ListingsPanel.IsExpanded = prefs.ListingsExpanded;
            ResultCurrency.SelectedIndex = Math.Clamp(prefs.Currency, 0, ResultCurrency.Items.Count - 1);
            SellerMode.SelectedIndex = Math.Clamp(prefs.Status, 0, SellerMode.Items.Count - 1);
            ListingAge.SelectedIndex = Math.Clamp(prefs.Age, 0, ListingAge.Items.Count - 1);
            BroadPreset.IsChecked = prefs.Broad; ExactPreset.IsChecked = !prefs.Broad;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { ResultStatus.Text = "Could not load evaluator preferences · defaults in use"; }
    }
    private void SavePreferences()
    {
        if (IsTestMode) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);
            File.WriteAllText(PreferencesPath + ".tmp", JsonSerializer.Serialize(new ViewPreferences(ResultCurrency.SelectedIndex, SellerMode.SelectedIndex, ListingAge.SelectedIndex, BroadPreset.IsChecked == true, ListingsPanel.IsExpanded)));
            File.Move(PreferencesPath + ".tmp", PreferencesPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ResultStatus.Text = "Could not save evaluator preferences"; }
    }
    public void VerifyLocalFilters()
    {
        if (drafts.Count == 0) throw new Exception("No editable filters generated");
        BroadPreset.IsChecked = true; ApplyPreset(this, new RoutedEventArgs());
        var first = drafts[0];
        var expected = first.Filter.CopiedValue!.Value - Math.Abs(first.Filter.CopiedValue.Value) * .1m;
        if (first.Min.Text != expected.ToString("0.##", CultureInfo.InvariantCulture)) throw new Exception("Broad preset failed");
        first.Filter.Enabled = true; first.Min.Text = "9"; first.Max.Text = "2";
        SaveDraft(this, new RoutedEventArgs());
        if (!ResultStatus.Text.Contains("Minimum cannot")) throw new Exception("Invalid filter range accepted");
        first.Min.Text = "2"; first.Max.Text = "9"; SaveDraft(this, new RoutedEventArgs());
        var oldItem = item!; SetItem(null, "test"); SetItem(oldItem, "test");
        if (drafts[0].Min.Text != "2" || drafts[0].Max.Text != "9" || !drafts[0].Filter.Enabled) throw new Exception("Local filter draft lost");
    }
    public void VerifyPriceWorkflow()
    {
        string Text(int life) => $"Item Class: Rings\nRarity: Rare\nUI Price Test\nGold Ring\n--------\nItem Level: 80\n+{life} to maximum Life";
        var now = DateTimeOffset.UtcNow;
        SetItem(ItemParser.Parse(Text(100))!, "UI price test"); SetPreferredCurrency("Exalted Orb");
        SellerMode.SelectedIndex = 1; ListingAge.SelectedIndex = 0;
        var lifeRow = drafts.First(x => x.Filter.Text == "+100 to maximum Life");
        if (!lifeRow.Filter.Enabled) rowToggles[lifeRow.Filter.GroupId](); lifeRow.Min.Text = "110"; lifeRow.Max.Clear();
        var dataset = new ListingDocument("UI price test", "UI TEST DATA", now,
            new[] { 90, 110, 120, 130 }.Select((life, i) => new ImportedListing("ui-" + i, "test-seller-" + i, new ListingPrice((i + 1) * 10, "Exalted Orb"), Text(life), now, true, true)).ToArray());
        var market = ComparableMarket.Parse(JsonSerializer.Serialize(dataset), now);
        var result = market.Search(BuildComparableRequest("UI price test"), now);
        SetComparableResult(result);
        if (result.Rows.Count != 3 || result.Median != 30 || Estimate.Text != "—") throw new Exception("UI filter-to-price flow failed");
        lifeRow.Min.Text = "121";
        if (Estimate.Text != "—" || Variants.ItemsSource != null) throw new Exception("Filter edit retained stale prices");
        var narrower = market.Search(BuildComparableRequest("UI price test"), now);
        if (narrower.Rows.Count != 1 || narrower.Median != null) throw new Exception("Updated UI bound not applied");
        SetComparableResult(narrower);
        SetItem(ItemParser.Parse(Text(120))!, "UI price test"); SetComparableResult(result);
        if (!Estimate.Text.Contains("30 Exalted Orb")) throw new Exception("Similar listings did not yield a recommendation");
        SetItem(ItemParser.Parse("Item Class: Rings\nRarity: Rare\nOther Ring\nGold Ring\n--------\n+50 to Dexterity")!, "UI price test");
        SetComparableResult(result);
        if (Estimate.Text != "—") throw new Exception("Unrelated base listings presented as an item estimate");
        SetPreferredCurrency("Divine Orb");
        if (Variants.ItemsSource != null || market.Search(BuildComparableRequest("UI price test"), now).Rows.Count != 0) throw new Exception("Currency switch retained incompatible prices");
    }
    public void VerifyScrollableLayout()
    {
        Width = 480; Height = 550; UpdateLayout();
        foreach (var expander in valueExpanders) expander.IsExpanded = true;
        UpdateLayout();
        if (BodyScroll.ScrollableHeight <= 0) throw new Exception("Long item has no scrollable content");
        var footer = CheckPricesButton.TransformToAncestor(this).Transform(new Point());
        if (footer.Y + CheckPricesButton.ActualHeight > ActualHeight || footer.Y < 0) throw new Exception("Price action is outside the window");
        foreach (var row in drafts)
        {
            var position = row.Max.TransformToAncestor(SideScroll).Transform(new Point());
            if (position.X + row.Max.ActualWidth > SideScroll.ActualWidth + 1 || row.Min.ActualWidth < 60 || row.Min.ActualHeight < 28) throw new Exception("Filter field is clipped or too small");
        }
        BodyScroll.ScrollToTop(); UpdateLayout();
        BodyScroll.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = Mouse.PreviewMouseWheelEvent });
        UpdateLayout();
        if (BodyScroll.VerticalOffset <= 0) throw new Exception("Mouse wheel does not scroll the item");
        BodyScroll.ScrollToEnd(); UpdateLayout();
        if (Math.Abs(BodyScroll.VerticalOffset - BodyScroll.ScrollableHeight) > 1) throw new Exception("Cannot reach the bottom of the results");
        var field = drafts[0].Min;
        field.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, Environment.TickCount, null, field) { RoutedEvent = Keyboard.GotKeyboardFocusEvent });
        if (field.SelectedText != field.Text || !drafts[0].Filter.Enabled) throw new Exception("Keyboard focus does not select the value and enable its filter");
        FitToBounds(new Rect(0, 0, 640, 480), true);
        if (Left < 0 || Top < 0 || Left + Width > 640 || Top + Height > 480) throw new Exception("Evaluator exceeds a small work area");
        MaxHeight = MaxWidth = double.PositiveInfinity; MinHeight = 400; Width = 480; Height = 550;
        foreach (var expander in valueExpanders) expander.IsExpanded = false;
        ListingsPanel.IsExpanded = true; UpdateLayout();
        footer = CheckPricesButton.TransformToAncestor(this).Transform(new Point());
        if (footer.Y + CheckPricesButton.ActualHeight > ActualHeight) throw new Exception("Expanded listings push Search off screen");
        ListingsPanel.IsExpanded = false;
        BodyScroll.ScrollToTop(); UpdateLayout();
    }
    public void VerifyContentSizing()
    {
        var original = item!;
        MaxHeight = 900; Width = 620;
        SetItem(ItemParser.Parse("Rarity: Rare\nSmall Ring\nGold Ring\n--------\nItem Level: 80\n+100 to maximum Life"), "Size test");
        ResizeToItemContent(); UpdateLayout();
        double shortHeight = Height;
        SetItem(original, "Size test"); ResizeToItemContent(); UpdateLayout();
        if (Height <= shortHeight || Height > 900) throw new Exception("Item height does not adapt to content within its limit");
        var search = CheckPricesButton.TransformToAncestor(this).Transform(new Point(CheckPricesButton.ActualWidth / 2, 0));
        if (Math.Abs(search.X - BodyScroll.TransformToAncestor(this).Transform(new Point(BodyScroll.ActualWidth / 2,0)).X) > 1) throw new Exception("Search is not centered");
        MaxHeight = 1040; Width = 800; Height = 450;
        SetItem(ItemParser.Parse(File.ReadAllText("tests/Fixtures/quarterstaff.txt")), "Full weapon sizing");
        ListingsPanel.IsExpanded = true;
        ResizeToItemContent(); UpdateLayout();
        if (BodyScroll.ScrollableHeight > 1) throw new Exception($"Weapon overflow {BodyScroll.ScrollableHeight}; height {Height}/{ActualHeight}; viewport {BodyScroll.ViewportHeight}; desired {((FrameworkElement)BodyScroll.Content).DesiredSize.Height}; listings {ListingsScroll.ActualHeight}/{ListingsScroll.MaxHeight}");
        MaxHeight = double.PositiveInfinity;
    }
    private void ScrollBody(object sender, MouseWheelEventArgs e)
    {
        BodyScroll.ScrollToVerticalOffset(BodyScroll.VerticalOffset - e.Delta);
        e.Handled = true;
    }
    private void QueueContentResize()
    {
        if (!IsTestMode) Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(ResizeToItemContent));
    }
    public void ResizeToItemContent()
    {
        if (item == null) return;
        // A remembered manual height must never truncate the next item's stats.
        // Keep the chosen width, then give the item priority over the listings pane.
        if (!IsTestMode) FitToScreen(false);
        ListingsScroll.MaxHeight = 200;
        UpdateLayout();
        var content = (FrameworkElement)BodyScroll.Content;
        double width = Math.Max(1, BodyScroll.ActualWidth);
        content.Measure(new Size(width, double.PositiveInfinity));
        double chrome = Math.Max(150, ActualHeight - BodyScroll.ActualHeight);
        double limit = double.IsFinite(MaxHeight) ? MaxHeight : SystemParameters.WorkArea.Height - 24;
        double required = content.DesiredSize.Height + chrome + 4;
        if (ListingsPanel.IsExpanded && required > limit)
        {
            ListingsScroll.MaxHeight = Math.Max(64, 200 - (required - limit));
            UpdateLayout();
            content.Measure(new Size(width, double.PositiveInfinity));
            chrome = Math.Max(150, ActualHeight - BodyScroll.ActualHeight);
            required = content.DesiredSize.Height + chrome + 4;
        }
        Height = Math.Clamp(required, Math.Min(MinHeight, limit), limit);
        UpdateLayout();
        // Wrapping and the scrollbar gutter settle only after the window is arranged.
        // Resolve any remaining deficit from that actual layout rather than the old viewport.
        for (int pass = 0; pass < 3 && BodyScroll.ScrollableHeight > 1; pass++)
        {
            double deficit = BodyScroll.ScrollableHeight + 4;
            double growth = Math.Min(deficit, Math.Max(0, limit - Height));
            Height += growth;
            if (ListingsPanel.IsExpanded && deficit > growth)
                ListingsScroll.MaxHeight = Math.Max(64, ListingsScroll.MaxHeight - (deficit - growth));
            UpdateLayout();
        }
        if (BodyScroll.ScrollableHeight <= 1) BodyScroll.ScrollToTop();
        if (!IsTestMode) FitToScreen(false);
    }

    public void FitToScreen(bool reposition)
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        var screen = reposition ? System.Windows.Forms.Screen.FromPoint(cursor) : System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        var dpi = VisualTreeHelper.GetDpi(this);
        var area = screen.WorkingArea;
        FitToBounds(new Rect(area.X / dpi.DpiScaleX, area.Y / dpi.DpiScaleY, area.Width / dpi.DpiScaleX, area.Height / dpi.DpiScaleY), reposition);
    }
    private void FitToBounds(Rect area, bool reposition)
    {
        double availableWidth = Math.Max(1, area.Width - 24), availableHeight = Math.Max(1, area.Height - 24);
        MinWidth = Math.Min(480, availableWidth); MinHeight = Math.Min(400, availableHeight);
        MaxWidth = availableWidth; MaxHeight = availableHeight;
        Width = Math.Clamp(Width, MinWidth, MaxWidth); Height = Math.Clamp(Height, MinHeight, MaxHeight);
        Left = reposition ? area.Right - Width - 12 : Math.Clamp(Left, area.Left + 12, area.Right - Width - 12);
        Top = reposition ? area.Top + 12 : Math.Clamp(Top, area.Top + 12, area.Bottom - Height - 12);
    }
    private void DragPanel(object sender, DragDeltaEventArgs e) { Left += e.HorizontalChange; Top += e.VerticalChange; }
    private void EndDrag(object sender, DragCompletedEventArgs e) { FitToScreen(false); ResizeToItemContent(); }
}
