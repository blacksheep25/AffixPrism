using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ExileLens.Core;

namespace ExileLens;

public sealed class ListingComparisonWindow : Window
{
    public string Yours { get; }
    public string Seller { get; }
    public string Changes { get; }
    public ListingComparisonWindow(CopiedItem yours, CopiedItem other) : this(yours,
        new ComparableListing(new ImportedListing("pinned", "Pinned comparison", new ListingPrice(0,""), other.Details, DateTimeOffset.Now, false, false), other, ItemAnalysis.From(other)), null, "Pinned item → current item") { }
    public ListingComparisonWindow(CopiedItem yours, ComparableListing listing, string? yourIcon = null, string? heading = null)
    {
        WindowPlacement.Attach(this,"comparison");
        Yours = yours.Details;
        Seller = listing.Item.Details;
        Changes = ItemComparison.Describe(yours, listing.Item);
        Title = "Exile Lens · Compare · " + listing.Account;
        Width = Math.Min(900, SystemParameters.WorkArea.Width - 24);
        Height = Math.Min(720, SystemParameters.WorkArea.Height - 24);
        MinWidth = Math.Min(480, Width); MinHeight = Math.Min(400, Height);
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.CanResizeWithGrip;
        Foreground = new SolidColorBrush(Color.FromRgb(217, 210, 188)); FontFamily = new FontFamily("Segoe UI"); FontSize = 12;
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var title = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        title.ColumnDefinitions.Add(new ColumnDefinition()); title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        title.Children.Add(new TextBlock { Text = heading ?? listing.Account + " · " + listing.PriceLabel, Margin = new Thickness(8), Foreground = new SolidColorBrush(Color.FromRgb(191,165,109)) });
        var drag = new Thumb { Cursor = Cursors.SizeAll, Background = Brushes.Transparent, Opacity = 0 };
        drag.DragDelta += (_, e) => { Left += e.HorizontalChange; Top += e.VerticalChange; };
        title.Children.Add(drag);
        var close = new Button { Content = "Close", Padding = new Thickness(10,4,10,4) }; close.Click += (_, _) => Close();
        Grid.SetColumn(close, 1); title.Children.Add(close); grid.Children.Add(title);
        var content = new StackPanel { Margin = new Thickness(0,0,10,0) };
        var keyStats = DefaultItemFilters.Suggested(yours);
        var keyChanges = ItemComparison.Rows(yours,listing.Item).Where(r=>keyStats.Contains(r.Yours)).ToArray();
        if(keyChanges.Length>0)
        {
            var summary = new StackPanel { Margin=new Thickness(8,4,8,12) };
            summary.Children.Add(new TextBlock { Text="Key comparison · yours → seller", FontWeight=FontWeights.SemiBold, Foreground=Foreground });
            foreach(var stat in keyChanges) summary.Children.Add(new TextBlock { Text=stat.Yours+"  →  "+stat.Seller, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,4,0,0), Foreground=Foreground });
            content.Children.Add(summary);
        }
        content.Children.Add(new TextBlock { Text=SimilarItems.Explain(yours,listing.Item), TextWrapping=TextWrapping.Wrap, Foreground=Foreground, Margin=new Thickness(8,4,8,8) });
        var cards = new Grid(); cards.ColumnDefinitions.Add(new ColumnDefinition()); cards.ColumnDefinitions.Add(new ColumnDefinition());
        cards.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
        cards.RowDefinitions.Add(new RowDefinition());
        var yoursLabel=new TextBlock { Text="YOUR ITEM", FontSize=14, FontWeight=FontWeights.SemiBold, Foreground=Brushes.PaleTurquoise, Margin=new Thickness(12,6,12,8) };
        var sellerLabel=new TextBlock { Text=listing.Listing.Id=="average" ? "SIMILAR-ITEM AVERAGE" : listing.Listing.Id=="pinned" ? "COMPARISON ITEM" : "SELLER’S ITEM", FontSize=14, FontWeight=FontWeights.SemiBold, Foreground=new SolidColorBrush(Color.FromRgb(220,199,140)), Margin=new Thickness(12,6,12,8) };
        cards.Children.Add(yoursLabel); Grid.SetColumn(sellerLabel,1); cards.Children.Add(sellerLabel);
        var own=new ItemPreviewCard { Item = yours, ComparedWith = listing.Item, IconUrl = yourIcon, Margin = new Thickness(5) }; Grid.SetRow(own,1); cards.Children.Add(own);
        var other = new ItemPreviewCard { Item = listing.Item, ComparedWith = yours, IconUrl = listing.IconUrl, Margin = new Thickness(5) }; Grid.SetColumn(other, 1); Grid.SetRow(other,1); cards.Children.Add(other); content.Children.Add(cards);
        var numbers = new StackPanel();
        content.Children.Add(new Expander { Header = "Numerical changes · seller minus yours", Foreground = new SolidColorBrush(Color.FromRgb(191,165,109)), IsExpanded = false, Content = numbers, Margin = new Thickness(5,10,5,0) });
        var differences = new CheckBox { Content = "Differences only", Foreground = new SolidColorBrush(Color.FromRgb(220,199,140)), Margin = new Thickness(8,12,8,8) }; numbers.Children.Add(differences);
        var headings = Columns(); AddCell(headings,"Your stats",0,true); AddCell(headings,"Seller stats",1,true); AddCell(headings,"Change",2,true); numbers.Children.Add(headings);
        var rows = new StackPanel(); numbers.Children.Add(rows);
        var values = ItemComparison.Rows(yours,listing.Item);
        void RenderRows()
        {
            rows.Children.Clear();
            foreach (var value in values.Where(x => differences.IsChecked != true || x.Changed))
            {
                var row = Columns(); AddCell(row,value.Yours,0,value.Kind == "Property",value.Kind,value.YoursMetadata); AddCell(row,value.Seller,1,value.Kind == "Property",value.Kind,value.SellerMetadata); AddCell(row,value.Change,2,true);
                rows.Children.Add(new Border { Background = new SolidColorBrush(value.Changed ? Color.FromRgb(30,27,19) : Color.FromRgb(18,18,16)), BorderBrush = new SolidColorBrush(Color.FromRgb(52,47,36)), BorderThickness = new Thickness(0,0,0,1), Child = row });
            }
            if (rows.Children.Count == 0) rows.Children.Add(new TextBlock { Text = "No differences in the available stats.", Margin = new Thickness(12) });
        }
        differences.Checked += (_, _) => RenderRows(); differences.Unchecked += (_, _) => RenderRows(); RenderRows();
        var scroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        scroll.Resources.Add(typeof(ScrollBar), System.Windows.Markup.XamlReader.Parse("""
<Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ScrollBar"><Setter Property="Width" Value="7"/><Setter Property="Opacity" Value="0.5"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar"><Track x:Name="PART_Track" IsDirectionReversed="True" Orientation="{TemplateBinding Orientation}"><Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0"/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="#92866B" CornerRadius="3" Margin="1,0"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0"/></Track.IncreaseRepeatButton></Track></ControlTemplate></Setter.Value></Setter><Style.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter Property="Opacity" Value="1"/></Trigger></Style.Triggers></Style>
"""));
        Grid.SetRow(scroll,1); grid.Children.Add(scroll);
        scroll.Resources["ComparisonScrollBar"] = scroll.Resources[typeof(ScrollBar)];
        scroll.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ScrollViewer">
<Grid><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width="7"/></Grid.ColumnDefinitions>
<ScrollContentPresenter x:Name="PART_ScrollContentPresenter" Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}" CanContentScroll="{TemplateBinding CanContentScroll}"/>
<ScrollBar x:Name="PART_VerticalScrollBar" Grid.Column="1" Width="7" Style="{DynamicResource ComparisonScrollBar}" Orientation="Vertical" Maximum="{TemplateBinding ScrollableHeight}" ViewportSize="{TemplateBinding ViewportHeight}" Value="{Binding VerticalOffset, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}" Visibility="{TemplateBinding ComputedVerticalScrollBarVisibility}"/>
</Grid></ControlTemplate>
""");
        var note = new TextBlock { Text = "Changes show seller minus yours, not an upgrade rating. Missing stats mean not present in the available item data.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(158,151,135)), Margin = new Thickness(6,10,6,2) };
        Grid.SetRow(note,2); grid.Children.Add(note);
        Content = new Border { Background = new SolidColorBrush(Color.FromRgb(16,16,14)), BorderBrush = new SolidColorBrush(Color.FromRgb(91,79,49)), BorderThickness = new Thickness(1), Padding = new Thickness(10), Child = grid };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }
    private static Grid Columns()
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) }); return grid;
    }
    private static void AddCell(Grid grid,string text,int column,bool muted,string? kind = null,string? metadata = null)
    {
        var cell = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10,9,10,9), FontFamily = new FontFamily("Georgia"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(muted ? Color.FromRgb(205,194,159) : Color.FromRgb(170,173,243)) };
        if (kind != null) cell.Foreground = ItemTextStyle.Brush(new ItemLine(text,kind,metadata));
        Grid.SetColumn(cell,column); grid.Children.Add(cell);
    }
    private static FrameworkElement Header(string title,CopiedItem item,string? icon)
    {
        var panel = new StackPanel { Margin = new Thickness(5) };
        panel.Children.Add(new TextBlock { Text = title, Margin = new Thickness(0,4,0,8), Foreground = new SolidColorBrush(Color.FromRgb(191,165,109)) });
        panel.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(33,29,17)), BorderBrush = new SolidColorBrush(Color.FromRgb(118,101,54)), BorderThickness = new Thickness(0,1,0,1), Padding = new Thickness(8), Child = new TextBlock { Text = item.Name + "\n" + item.BaseType, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontFamily = new FontFamily("Georgia"), FontSize = 17, Foreground = new SolidColorBrush(Color.FromRgb(233,221,135)) } });
        panel.Children.Add(new RemoteItemIcon { Url = icon, Height = 80, Stretch = Stretch.Uniform, Margin = new Thickness(0,10,0,4) });
        return panel;
    }
    private static FrameworkElement VisualCard(string title, CopiedItem item, string? icon)
    {
        var grid = new Grid { Margin = new Thickness(5) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition());
        grid.Children.Add(new TextBlock { Text = title, Foreground = new SolidColorBrush(Color.FromRgb(191,165,109)), Margin = new Thickness(0,4,0,8) });
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = new ItemPreviewCard { Item = item, IconUrl = icon } };
        Grid.SetRow(scroll, 1); grid.Children.Add(scroll); return grid;
    }
    private static string Display(CopiedItem item) => string.Join(Environment.NewLine,
        new[] { item.Name, item.BaseType != item.Name ? item.BaseType : "", "Rarity: " + item.Rarity, "" }
        .Concat(ItemAnalysis.From(item).Lines.Select(x => x.Text)));
    private static FrameworkElement Card(string heading, string text)
    {
        var grid = new Grid { Margin = new Thickness(5) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition());
        grid.Children.Add(new TextBlock { Text = heading, Margin = new Thickness(0,4,0,8), Foreground = new SolidColorBrush(Color.FromRgb(191,165,109)), TextWrapping = TextWrapping.Wrap });
        var details = new TextBox { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalContentAlignment = VerticalAlignment.Top, Padding = new Thickness(10), FontFamily = new FontFamily("Georgia"), FontSize = 13 };
        Grid.SetRow(details, 1); grid.Children.Add(details);
        return grid;
    }
}
