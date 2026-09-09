using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ExileLens.Core;
namespace ExileLens;
public sealed class SocketStrip : StackPanel
{
    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(nameof(Item),typeof(CopiedItem),typeof(SocketStrip),new PropertyMetadata(null,Changed));
    public CopiedItem? Item { get => (CopiedItem?)GetValue(ItemProperty); set => SetValue(ItemProperty,value); }
    public SocketStrip() { Orientation=Orientation.Horizontal; HorizontalAlignment=HorizontalAlignment.Center; }
    private static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e) => ((SocketStrip)d).Render();
    private void Render()
    {
        Children.Clear();
        if(Item == null) return;
        foreach(var socket in ItemSockets.From(Item))
        {
            string label=socket.Occupied == false ? "Empty socket" : socket.Occupied == null ? "Socket contents unresolved" : socket.Name ?? "Rune effect detected";
            Border Details() {
                var panel = new StackPanel();
                panel.Children.Add(new TextBlock { Text=label, Foreground=Brushes.PaleTurquoise, FontSize=15, TextWrapping=TextWrapping.Wrap, FontWeight=FontWeights.SemiBold });
                if(socket.IconUrl != null) panel.Children.Add(new RemoteItemIcon { Url=socket.IconUrl,Width=64,Height=64 });
                panel.Children.Add(new TextBlock { Text=socket.Details ?? "", Foreground=Brushes.LightSteelBlue,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0) });
                panel.Children.Add(new TextBlock { Text=socket.Inferred ? "Matched from copied effects; individual socket contents are not confirmed." : "Socket information supplied by the item source.",Foreground=Brushes.DarkGray,TextWrapping=TextWrapping.Wrap,FontSize=11,Margin=new Thickness(0,8,0,0) });
                if(socket.IconUrl == null && socket.Occupied == true) panel.Children.Add(new TextBlock { Text="Artwork unavailable",Foreground=Brushes.DarkGray,FontSize=11 });
                return new Border { Child=panel,Width=300,Padding=new Thickness(12),Background=new SolidColorBrush(Color.FromRgb(18,20,19)),BorderBrush=Brushes.DarkKhaki,BorderThickness=new Thickness(1) };
            }
            var contents=new Grid();
            contents.Children.Add(new TextBlock { Text=socket.Occupied == null ? "?" : socket.Occupied == false ? "" : "◆", Foreground=Brushes.LightSteelBlue, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center, FontSize=20 });
            contents.Children.Add(new RemoteItemIcon { Url=socket.IconUrl, Width=30, Height=30 });
            var button = new Button { Width=40,Height=40,Padding=new Thickness(0),BorderThickness=new Thickness(2),BorderBrush=new SolidColorBrush(Color.FromRgb(159,141,89)),Background=new SolidColorBrush(Color.FromRgb(13,17,18)),Margin=new Thickness(4,6,4,6),Content=contents,Cursor=Cursors.Hand };
            var tooltip = new ToolTip { Content=Details(),PlacementTarget=button,Placement=PlacementMode.Bottom,Padding=new Thickness(0),BorderThickness=new Thickness(0) };
            var popup = new Popup { Child=Details(),PlacementTarget=button,Placement=PlacementMode.Bottom,StaysOpen=false,AllowsTransparency=true };
            button.MouseEnter += (_,_) => { if(!popup.IsOpen) tooltip.IsOpen=true; };
            button.MouseLeave += (_,_) => tooltip.IsOpen=false;
            button.Click += (_,_) => { tooltip.IsOpen=false; popup.IsOpen=true; };
            button.Unloaded += (_,_) => { tooltip.IsOpen=false; popup.IsOpen=false; };
            var group = new StackPanel { Width=104,Margin=new Thickness(3) };
            button.HorizontalAlignment = HorizontalAlignment.Center;
            group.Children.Add(button);
            if(socket.Name != null) group.Children.Add(new TextBlock { Text=socket.Name,Foreground=Brushes.PaleTurquoise,FontSize=10,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap });
            Children.Add(group);
        }
    }
}
