using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExileLens.Core;

namespace ExileLens;

public sealed class RemoteItemIcon : Image
{
    public RemoteItemIcon() { Visibility = Visibility.Collapsed; Stretch = Stretch.Uniform; }
    private static readonly Dictionary<string, BitmapImage> images = new();
    public static readonly DependencyProperty UrlProperty = DependencyProperty.Register(nameof(Url), typeof(string), typeof(RemoteItemIcon), new PropertyMetadata(null, Changed));
    public string? Url { get => (string?)GetValue(UrlProperty); set => SetValue(UrlProperty, value); }
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        var image = (RemoteItemIcon)target;
        var url = ItemArtwork.SafeUrl(e.NewValue as string);
        image.Source = null;
        image.Visibility = url == null ? Visibility.Collapsed : Visibility.Visible;
        if (url == null) return;
        try
        {
            if (!images.TryGetValue(url, out var bitmap))
            {
                bitmap = new BitmapImage(new Uri(url));
                if (images.Count >= 100) images.Clear();
                images[url] = bitmap;
            }
            bitmap.DownloadFailed += (_, _) => { if (image.Url == url) { image.Source = null; image.Visibility = Visibility.Collapsed; } images.Remove(url); };
            image.Source = bitmap;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.IO.IOException or System.Net.WebException)
        { image.Visibility = Visibility.Collapsed; }
    }
}

public sealed class ItemPreviewCard : Border
{
    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(nameof(Item), typeof(CopiedItem), typeof(ItemPreviewCard), new PropertyMetadata(null, Refresh));
    public static readonly DependencyProperty IconUrlProperty = DependencyProperty.Register(nameof(IconUrl), typeof(string), typeof(ItemPreviewCard), new PropertyMetadata(null, Refresh));
    public CopiedItem? Item { get => (CopiedItem?)GetValue(ItemProperty); set => SetValue(ItemProperty, value); }
    public string? IconUrl { get => (string?)GetValue(IconUrlProperty); set => SetValue(IconUrlProperty, value); }
    private static void Refresh(DependencyObject target, DependencyPropertyChangedEventArgs _) => ((ItemPreviewCard)target).Render();
    private static Brush Color(byte r, byte g, byte b) => new SolidColorBrush(System.Windows.Media.Color.FromRgb(r,g,b));
    private void Render()
    {
        if (Item == null) { Child = null; return; }
        Background = Color(16,16,13); BorderThickness = new Thickness(1); Padding = new Thickness(10);
        var rarity = Item.Rarity switch { "Rare" => Color(233,221,135), "Unique" => Color(209,131,61), "Magic" => Color(145,151,235), _ => Color(205,200,183) };
        bool gem = ItemPresentation.IsGem(Item);
        if (gem) rarity = Color(108,201,195);
        if(ItemPresentation.IsQuest(Item)) rarity=Color(74,230,58);
        BorderBrush = Color(49,46,32);
        var stack = new StackPanel();
        TextBlock Text(string text, Brush brush, double size = 13) => new() { Text = text, Foreground = brush, FontFamily = new FontFamily("Georgia"), FontSize = size, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2,3,2,3) };
        var header = new StackPanel(); header.Children.Add(Text(Item.Name, rarity, 17));
        if (gem) header.Children.Add(Text(Item.ItemClass.Contains("Support",StringComparison.OrdinalIgnoreCase) || Item.Details.Contains("Support,") ? "Support" : "Skill Gem",rarity,15));
        else if (Item.BaseType != Item.Name) header.Children.Add(Text(Item.BaseType, rarity, 15));
        if (ItemAnalysis.From(Item).Desecrated) header.Children.Insert(0, Text("◉  DESECRATED  ◉", Color(178,209,83), 11));
        var nameplate = new Grid();
        nameplate.Children.Add(header);
        var flourish = "M 2 0 C 18 0 18 12 7 12 C 0 12 0 20 14 20 M 8 3 L 15 10 L 8 17";
        nameplate.Children.Add(new System.Windows.Shapes.Path { Data=Geometry.Parse(flourish),Stroke=rarity,StrokeThickness=1,Width=18,Height=22,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left,IsHitTestVisible=false });
        nameplate.Children.Add(new System.Windows.Shapes.Path { Data=Geometry.Parse(flourish),Stroke=rarity,StrokeThickness=1,Width=18,Height=22,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right,RenderTransform=new ScaleTransform(-1,1,9,11),IsHitTestVisible=false });
        stack.Children.Add(new Border { Background = Color(25,23,15), BorderBrush = rarity, BorderThickness = new Thickness(1), CornerRadius=new CornerRadius(2), Padding = new Thickness(4), Child = nameplate });
        stack.Children.Add(new RemoteItemIcon { Url = IconUrl, Height = 44, MaxWidth = 44, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,8,0,4) });
        stack.Children.Add(new SocketStrip { Item = Item });
        string? previous = null;
        foreach (var line in gem ? ItemAnalysis.From(Item).Lines : ItemPresentation.Lines(Item))
        {
            // Desecration is already shown by the shared nameplate, whether it was
            // inferred from modifier metadata or supplied as a standalone trade flag.
            if (line.Kind == "State" && line.Text == "Desecrated") continue;
            if (previous != null && previous != line.Kind) stack.Children.Add(new Border { Height = 1, Background = Color(73,61,31), Margin = new Thickness(40,7,40,7) });
            var label = Text(ItemTextStyle.Display(line), ItemTextStyle.Brush(line));
            if (line.Kind is "Flavour" or "Gem description" or "Instructions") label.FontStyle = FontStyles.Italic;
            label.ToolTip = ItemMetadata.Style(line).Name + "\n" + line.Tooltip;
            stack.Children.Add(label);
            previous = line.Kind;
        }
        Child = stack;
    }
}

public static class ItemTextStyle
{
    public static string Display(ItemLine line) => line.Text == "Twice Corrupted" ? "☠ Twice Corrupted" : line.Text;
    public static Brush Brush(ItemLine line) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(ItemMetadata.Style(line).Colour));
}
