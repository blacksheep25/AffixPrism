using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AffixPrism;
internal sealed class RuneRegionSelector : Window
{
    private Point start;
    private readonly Canvas canvas = new();
    private readonly Rectangle outline = new() { Stroke = Brushes.Gold, StrokeThickness = 2, Fill = new SolidColorBrush(Color.FromArgb(35,255,255,255)) };
    public System.Drawing.Rectangle? Region { get; private set; }
    public RuneRegionSelector()
    {
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(60,0,0,0)); Topmost = true; ShowInTaskbar = false;
        Left = SystemParameters.VirtualScreenLeft; Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth; Height = SystemParameters.VirtualScreenHeight;
        Cursor = Cursors.Cross; Content = canvas; canvas.Children.Add(outline);
        canvas.Children.Add(new TextBlock { Text = "Drag around the rune choice NAMES · Esc cancels", Foreground = Brushes.Gold, Background = Brushes.Black, FontSize = 22, Margin = new Thickness(30) });
        KeyDown += (_,e) => { if(e.Key == Key.Escape) Close(); };
        MouseLeftButtonDown += (_,e) => { start=e.GetPosition(this); CaptureMouse(); };
        MouseMove += (_,e) => { if(!IsMouseCaptured) return; var p=e.GetPosition(this); Canvas.SetLeft(outline,Math.Min(start.X,p.X)); Canvas.SetTop(outline,Math.Min(start.Y,p.Y)); outline.Width=Math.Abs(p.X-start.X); outline.Height=Math.Abs(p.Y-start.Y); };
        MouseLeftButtonUp += (_,e) => {
            if(!IsMouseCaptured) return;
            var a=PointToScreen(start); var b=PointToScreen(e.GetPosition(this)); ReleaseMouseCapture();
            Region=new System.Drawing.Rectangle((int)Math.Min(a.X,b.X),(int)Math.Min(a.Y,b.Y),(int)Math.Abs(b.X-a.X),(int)Math.Abs(b.Y-a.Y)); Close();
        };
    }
}
