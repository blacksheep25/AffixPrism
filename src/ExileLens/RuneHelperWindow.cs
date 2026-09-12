using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ExileLens.Core;

namespace ExileLens;
internal sealed class RuneHelperWindow : Window
{
    private readonly EconomyClient economy;
    private readonly bool test;
    private readonly RunePricesOverlay overlay = new();
    private readonly DispatcherTimer timer = new() { Interval=TimeSpan.FromMilliseconds(250) };
    private readonly TextBlock status = new() { TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,10) };
    private readonly TextBlock priceStatus = new() { TextWrapping=TextWrapping.Wrap,FontSize=11,Margin=new Thickness(0,8,0,8) };
    private readonly TextBlock regionText = new() { TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8) };
    private readonly TextBox debug = new() { IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=90 };
    private readonly ItemsControl results = new();
    private readonly Expander recognitionDetails = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly Button start = new() { Content="Start live prices" };
    private readonly Button refresh = new() { Content="Refresh prices" };
    private System.Drawing.Rectangle? region;
    private List<EconomyRow> catalog = new();
    private string league = "";
    private DateTimeOffset fetched;
    private DateTimeOffset nextRefresh;
    private DateTimeOffset nextScan;
    private bool running,busy,closing;
    private ContentControl? embeddedHost;
    private object? sharedContent;
    private Window PresentationWindow => embeddedHost!=null ? Window.GetWindow(embeddedHost) ?? this : this;
    private int generation;
    private string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ExileLens","rune-region.json");
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);

    public RuneHelperWindow(EconomyClient economy, bool test=false)
    {
        this.economy=economy; this.test=test;
        Title="Exile Lens · Rune choice helper"; Width=490; Height=610; MinWidth=420; MinHeight=480;
        Background=new SolidColorBrush(Color.FromRgb(17,17,14)); Foreground=new SolidColorBrush(Color.FromRgb(216,207,182)); Topmost=true; ShowInTaskbar=false;
        WindowStyle=WindowStyle.None; AllowsTransparency=true; ResizeMode=ResizeMode.CanResizeWithGrip;
        results.Foreground=Foreground;
        debug.Foreground=Foreground; debug.Background=Background; debug.CaretBrush=Brushes.Khaki;
        recognitionDetails.Foreground=Foreground;
        recognitionDetails.Header=new TextBlock { Text="Recognition details",Foreground=Brushes.Khaki };
        recognitionDetails.Content=debug; recognitionDetails.Margin=new Thickness(0,12,0,0);
        var body=new StackPanel();
        body.Children.Add(new TextBlock { Text="Open the Runeshape menu, select just its choice names, then start. English OCR runs locally while POE2 is foreground. Open this helper again from the tray to pause.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,8) });
        var buttons=new WrapPanel(); body.Children.Add(buttons);
        var select=new Button { Content="Select choice region" }; select.Click+=SelectRegion; buttons.Children.Add(select);
        start.Click+=StartScanning; buttons.Children.Add(start);
        refresh.Click+=async (_,_)=>await LoadPrices(); buttons.Children.Add(refresh);
        body.Children.Add(regionText); body.Children.Add(priceStatus); body.Children.Add(status);
        body.Children.Add(new TextBlock { Text="Latest detected choices",FontSize=16 });
        body.Children.Add(new ScrollViewer { Content=results,MaxHeight=210,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });
        body.Children.Add(recognitionDetails);
        body.Children.Add(new TextBlock { Text="Prices are per item and indicative. Approximate names need checking. Screen images stay in memory; no captures are saved or uploaded.",TextWrapping=TextWrapping.Wrap,FontSize=11,Margin=new Thickness(0,12,0,0) });
        var layout=new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) });
        var header=new DockPanel { Background=Background,Margin=new Thickness(0,0,0,10) };
        var close=new Button { Content="✕",ToolTip="Close helper and pause scanning",Padding=new Thickness(9,5,9,5),VerticalAlignment=VerticalAlignment.Top };
        close.Click+=(_,_)=> { Stop(); PresentationWindow.Hide(); }; DockPanel.SetDock(close,Dock.Right); header.Children.Add(close);
        var title=new TextBlock { Text="Rune choice prices",FontSize=22,Foreground=Brushes.Khaki,Padding=new Thickness(0,5,0,8),Cursor=System.Windows.Input.Cursors.SizeAll };
        title.MouseLeftButtonDown+=(_,e)=> { if(e.LeftButton==System.Windows.Input.MouseButtonState.Pressed) { PresentationWindow.DragMove(); e.Handled=true; } };
        header.Children.Add(title); layout.Children.Add(header);
        var scroll=new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        Grid.SetRow(scroll,1); layout.Children.Add(scroll);
        Content=new Border { Child=layout,Background=Background,BorderBrush=new SolidColorBrush(Color.FromRgb(107,92,48)),BorderThickness=new Thickness(1),Padding=new Thickness(16) };
        sharedContent=Content;
        timer.Tick+=async (_,_)=>await Scan();
        Closing+=(_,e)=> { Stop(); if(!closing) { e.Cancel=true; Hide(); } };
        Closed+=(_,_)=> { lifetime.Cancel(); overlay.Close(); };
        if(!test) try { if(File.Exists(ConfigPath)) region=JsonSerializer.Deserialize<System.Drawing.Rectangle>(File.ReadAllText(ConfigPath)); } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException) { }
        RegionLabel(); WindowPlacement.Attach(this,"rune-helper");
    }
    public void Open(string selectedLeague)
    {
        if(embeddedHost!=null) { embeddedHost.Content=null; embeddedHost=null; Content=sharedContent; }
        Stop();
        if(league!=selectedLeague) { league=selectedLeague; catalog.Clear(); results.ItemsSource=null; fetched=default; nextRefresh=default; priceStatus.Text="Prices not loaded for "+league; }
        Show(); Activate();
        if(catalog.Count==0 && !test) _=LoadPrices();
    }
    public void Shutdown() { closing=true; Stop(); lifetime.Cancel(); Close(); }
    internal void ShowRecognitionFixture()
    {
        recognitionDetails.IsExpanded=true;
        debug.Text="96% · 2x Runic Alloy\n92% · Greater Iron Rune";
        results.ItemsSource=new[] { "Runic Alloy · UI fixture price", "Greater Iron Rune · UI fixture price" };
    }
    public void Embed(ContentControl host,string selectedLeague)
    {
        Stop();
        if(league!=selectedLeague) { league=selectedLeague; catalog.Clear(); results.ItemsSource=null; fetched=default; nextRefresh=default; priceStatus.Text="Prices not loaded for "+league; }
        if(embeddedHost!=null) embeddedHost.Content=null;
        Hide(); Content=null; embeddedHost=host; host.Content=sharedContent;
        if(catalog.Count==0 && !test) _=LoadPrices();
    }
    private void Stop() { running=false; generation++; timer.Stop(); overlay.Hide(); status.Text="Paused · no screen capture"; }
    private void RegionLabel() => regionText.Text=region is { } r ? $"Region: {r.Width} × {r.Height} px · reselect if you move the game or change UI scale." : "No region selected.";
    private void SelectRegion(object sender,RoutedEventArgs e)
    {
        Stop(); PresentationWindow.Hide();
        var selector=new RuneRegionSelector(); selector.ShowDialog();
        if(selector.Region is { } r)
        {
            if(r.Width is <50 or >1600 || r.Height is <30 or >1200) status.Text="Choose only the names: 50–1600 px wide and 30–1200 px high.";
            else { region=r; if(!test) try { Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!); File.WriteAllText(ConfigPath,JsonSerializer.Serialize(r)); } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { status.Text="Region selected, but could not be saved."; } }
        }
        RegionLabel(); PresentationWindow.Show(); PresentationWindow.Activate();
    }
    private async void StartScanning(object sender,RoutedEventArgs e)
    {
        if(region==null) { status.Text="Select the choice-name region first."; return; }
        int version=generation;
        if(catalog.Count==0) await LoadPrices();
        if(catalog.Count==0 || closing || version!=generation) return;
        running=true; generation++; status.Text="Scanning · return to POE2"; PresentationWindow.Hide(); timer.Start();
    }
    private async Task LoadPrices()
    {
        if(!refresh.IsEnabled || test) return;
        refresh.IsEnabled=false; start.IsEnabled=false;
        string requestedLeague=league;
        try
        {
            var rows=new List<EconomyRow>(); var times=new List<DateTimeOffset>(); var failures=new List<string>(); bool stale=false;
            foreach(string category in new[] { "Runes","Currency","UncutGems","Expedition","Ritual","Breach","Verisium","Idols","SoulCores","Essences","LineageSupportGems","Abyss","Fragments" })
            {
                status.Text="Loading market cache · "+category;
                try { var data=await economy.CategoryAsync(new(category,true),requestedLeague,lifetime.Token,TimeSpan.FromMinutes(15)); rows.AddRange(data.Rows); times.Add(data.FetchedAt); stale|=data.Stale; }
                catch(HttpRequestException) { failures.Add(category); }
            }
            if(requestedLeague!=league || closing) return;
            catalog=rows.GroupBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).Where(x=>x.Count()==1).Select(x=>x.Single()).ToList();
            fetched=times.Count>0 ? times.Min() : default;
            priceStatus.Text=$"{league} · poe.ninja · {catalog.Count} names"+(fetched!=default ? $" · {fetched.ToLocalTime():dd MMM HH:mm}{(stale ? " · STALE" : "")}" : "")+(failures.Count>0 ? " · unavailable: "+string.Join(", ",failures) : "");
            nextRefresh=DateTimeOffset.Now.AddMinutes(15);
            if(catalog.Count==0) status.Text="No prices available. Check league and connection, then refresh.";
        }
        catch(OperationCanceledException) { }
        catch(Exception ex) when(ex is IOException or JsonException or ArgumentException or InvalidOperationException) { status.Text="Could not load rune prices: "+ex.Message; }
        finally { refresh.IsEnabled=true; start.IsEnabled=true; }
    }
    private async Task Scan()
    {
        if(!running || closing) return;
        var game=Native.ForegroundGameWindow();
        if(game==IntPtr.Zero) { overlay.Hide(); return; }
        if(DateTimeOffset.Now<nextScan) return;
        if(busy || region is not { } r) return;
        if(!GetWindowRect(game,out var bounds) || r.Left<bounds.Left || r.Top<bounds.Top || r.Right>bounds.Right || r.Bottom>bounds.Bottom)
        { overlay.Hide(); status.Text="Saved region is outside the game window. Reselect it."; return; }
        nextScan=DateTimeOffset.Now.AddMilliseconds(1500);
        busy=true; int version=generation;
        try
        {
            if(DateTimeOffset.Now>=nextRefresh) await LoadPrices();
            if(!running || version!=generation || Native.ForegroundGameWindow()!=game) return;
            var text=await Task.Run(()=>Native.ForegroundGameWindow()==game ? RuneOcr.Capture(r) : Array.Empty<RuneText>(),lifetime.Token);
            if(!running || version!=generation || Native.ForegroundGameWindow()!=game) { overlay.Hide(); return; }
            var matches=new List<(RuneText Text,RuneNameMatch Match)>();
            foreach(var line in text) if(RuneNames.Match(line.Text,line.Confidence,catalog) is { } match) matches.Add((line,match));
            debug.Text=string.Join("\n",text.Select(x=>$"{x.Confidence:0}% · {x.Text}"));
            results.ItemsSource=matches.Select(x=>$"{x.Match.Row.Name} · {x.Match.PriceLabel}{(x.Match.Approximate ? " · approximate name" : "")}").ToArray();
            status.Text=matches.Count==0 ? "No confidently matched names. Check the region and open the choice menu." : $"{matches.Count} choices detected · {DateTime.Now:HH:mm:ss}";
            if(!overlay.Render(r,matches,$"poe.ninja · {fetched.ToLocalTime():HH:mm}{(priceStatus.Text.Contains("STALE") ? " · STALE" : "")}")) status.Text="Not enough room beside the choice list. Move the game/menu or use these results.";
        }
        catch(OperationCanceledException) { }
        catch(Exception ex) when(ex is IOException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or Tesseract.TesseractException or DllNotFoundException or BadImageFormatException or System.Runtime.InteropServices.ExternalException)
        { Stop(); status.Text="Rune recognition stopped: "+ex.Message; if(!closing) Show(); }
        finally { busy=false; }
    }
}
