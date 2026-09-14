using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using AffixPrism.Core;

namespace AffixPrism;
internal sealed class RunePricesOverlay : Window
{
    private readonly Canvas canvas = new();
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd,int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd,int index,int value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
    public RunePricesOverlay()
    {
        WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; AllowsTransparency=true; Background=Brushes.Transparent;
        Topmost=true; ShowActivated=false; ShowInTaskbar=false; IsHitTestVisible=false; Focusable=false; Content=canvas;
        SourceInitialized += (_,_) => { var h=new WindowInteropHelper(this).Handle; SetWindowLong(h,-20,GetWindowLong(h,-20)|0x20|0x08000000|0x80); };
    }
    public bool Render(System.Drawing.Rectangle region, IReadOnlyList<(RuneText Text,RuneNameMatch Match)> rows, string stamp)
    {
        canvas.Children.Clear();
        if(rows.Count==0) { Hide(); return true; }
        var screen=System.Windows.Forms.Screen.FromRectangle(region).Bounds;
        const int width=300;
        int x=region.Right+8;
        if(x+width>screen.Right) x=region.Left-width-8;
        if(x<screen.Left) { Hide(); return false; }
        new WindowInteropHelper(this).EnsureHandle();
        Show();
        int top=Math.Max(screen.Top,region.Top-24);
        SetWindowPos(new WindowInteropHelper(this).Handle,new IntPtr(-1),x,top,width,Math.Min(region.Height+64,screen.Bottom-top),0x0010);
        var dpi=VisualTreeHelper.GetDpi(this);
        canvas.Children.Add(new TextBlock { Text=stamp,Foreground=Brushes.Khaki,Background=new SolidColorBrush(Color.FromArgb(245,18,18,14)),FontSize=10,Width=width/dpi.DpiScaleX,TextTrimming=TextTrimming.CharacterEllipsis });
        foreach(var row in rows)
        {
            var text=new TextBlock { Text=$"≈ {row.Match.PriceLabel}{(row.Match.Approximate ? " · check name" : "")}\n{row.Match.Row.Name}",Foreground=new SolidColorBrush(Theme.Colour("Gold")),FontSize=11,TextWrapping=TextWrapping.Wrap };
            var border=new Border { Child=text,Background=new SolidColorBrush(Color.FromArgb(245,18,18,14)),BorderBrush=new SolidColorBrush(Theme.Colour("Border")),BorderThickness=new Thickness(1),Padding=new Thickness(6,3,6,3),Width=width/dpi.DpiScaleX-2 };
            Canvas.SetTop(border,(row.Text.Y+region.Top-top)/dpi.DpiScaleY); canvas.Children.Add(border);
        }
        ToolTip=stamp;
        return true;
    }
}
