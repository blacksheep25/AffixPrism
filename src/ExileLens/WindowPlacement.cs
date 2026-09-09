using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
namespace ExileLens;
internal static class WindowPlacement
{
    private sealed record Saved(double Left,double Top,double Width,double Height,bool FixedSize);
    private sealed class Entry { public string Key=""; public bool Ready; public bool Fixed; public double StartWidth,StartHeight; public DispatcherTimer Timer=new() { Interval=TimeSpan.FromMilliseconds(600) }; }
    private static readonly Dictionary<Window,Entry> entries=new();
    private static string? testDirectory;
    private static string DirectoryPath => testDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ExileLens","windows");
    public static bool Disabled { get; set; }
    public static bool FixedSize(Window window) => entries.TryGetValue(window,out var e) && e.Fixed;
    public static void Attach(Window window,string key)
    {
        if(Disabled || entries.ContainsKey(window)) return;
        var entry=new Entry { Key=key }; entries[window]=entry;
        window.SourceInitialized += (_,_) => {
            try {
                var path=Path.Combine(DirectoryPath,key+".json");
                if(File.Exists(path)) {
                    var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(path));
                    if(saved != null && double.IsFinite(saved.Left) && double.IsFinite(saved.Top) && double.IsFinite(saved.Width) && double.IsFinite(saved.Height) && saved.Width>0 && saved.Height>0) {
                        window.WindowStartupLocation=WindowStartupLocation.Manual;
                        window.Left=saved.Left; window.Top=saved.Top; window.Width=Math.Max(window.MinWidth,saved.Width); window.Height=Math.Max(window.MinHeight,saved.Height); entry.Fixed=saved.FixedSize;
                    }
                }
            } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { }
            var source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            source?.AddHook((IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled) => {
                if(msg==0x231) { entry.StartWidth=window.Width; entry.StartHeight=window.Height; }
                if(msg==0x232) { if(Math.Abs(entry.StartWidth-window.Width)>1 || Math.Abs(entry.StartHeight-window.Height)>1) entry.Fixed=true; Clamp(window); Save(window,entry); }
                if(msg==0x7E) window.Dispatcher.BeginInvoke(new Action(()=>Clamp(window)));
                return IntPtr.Zero;
            });
        };
        window.Loaded += (_,_) => { Clamp(window); entry.Ready=true; };
        entry.Timer.Tick += (_,_) => { entry.Timer.Stop(); Save(window,entry); };
        void Schedule() { if(entry.Ready && window.IsVisible) { entry.Timer.Stop(); entry.Timer.Start(); } }
        window.LocationChanged += (_,_)=>Schedule(); window.SizeChanged += (_,_)=>Schedule();
        window.IsVisibleChanged += (_,_)=> { if(!window.IsVisible) Save(window,entry); else if(entry.Ready) Clamp(window); };
        window.Closed += (_,_)=> { entry.Timer.Stop(); Save(window,entry); entries.Remove(window); };
    }
    private static void Save(Window window,Entry entry)
    {
        if(!entry.Ready || window.WindowState != WindowState.Normal || !double.IsFinite(window.Left) || !double.IsFinite(window.Top)) return;
        try { System.IO.Directory.CreateDirectory(DirectoryPath); string path=Path.Combine(DirectoryPath,entry.Key+".json"); File.WriteAllText(path+".tmp",JsonSerializer.Serialize(new Saved(window.Left,window.Top,window.Width,window.Height,entry.Fixed))); File.Move(path+".tmp",path,true); }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { }
    }
    public static void Clamp(Window window)
    {
        var area=Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle).WorkingArea;
        var dpi=VisualTreeHelper.GetDpi(window);
        double left=area.Left/dpi.DpiScaleX,top=area.Top/dpi.DpiScaleY,width=area.Width/dpi.DpiScaleX,height=area.Height/dpi.DpiScaleY;
        window.MinWidth=Math.Min(window.MinWidth,width); window.MinHeight=Math.Min(window.MinHeight,height);
        window.Width=Math.Clamp(window.Width,window.MinWidth,width); window.Height=Math.Clamp(window.Height,window.MinHeight,height);
        window.Left=Math.Clamp(double.IsFinite(window.Left)?window.Left:left,left,left+width-window.Width);
        window.Top=Math.Clamp(double.IsFinite(window.Top)?window.Top:top,top,top+height-window.Height);
    }
    public static void Verify()
    {
        string directory=Path.Combine(Path.GetTempPath(),"ExileLens-placement-"+Guid.NewGuid().ToString("N"));
        bool wasDisabled=Disabled;
        Window? first=null,second=null;
        try {
            testDirectory=directory; Disabled=false;
            first=new Window { Width=420,Height=360,ShowInTaskbar=false };
            Attach(first,"fixture"); first.Show(); first.Left=80; first.Top=90; Clamp(first);
            double left=first.Left,top=first.Top; first.Hide(); first.Close(); first=null;
            second=new Window { Width=200,Height=200,ShowInTaskbar=false };
            Attach(second,"fixture"); second.Show();
            if(Math.Abs(second.Left-left)>1 || Math.Abs(second.Top-top)>1 || Math.Abs(second.Width-420)>1) throw new Exception("Window placement did not round-trip");
            second.Left=-100000; second.Top=-100000; Clamp(second);
            if(second.Left < -90000 || second.Top < -90000) throw new Exception("Off-screen placement was not recovered");
        }
        finally {
            first?.Close(); second?.Close(); Disabled=wasDisabled; testDirectory=null;
            if(File.Exists(Path.Combine(directory,"fixture.json"))) File.Delete(Path.Combine(directory,"fixture.json"));
            if(System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory);
        }
    }
    public static void Reset()
    {
        foreach(var pair in entries) {
            var window=pair.Key; var e=pair.Value; e.Fixed=false;
            window.Width=e.Key=="comparison"?900:e.Key=="evaluation"?800:600; window.Height=700;
            window.Left=SystemParameters.WorkArea.Left+40; window.Top=SystemParameters.WorkArea.Top+40;
            Clamp(window); Save(window,e);
        }
    }
}
