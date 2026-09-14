using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace AffixPrism;
internal static class TrayMenuPlacement
{
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MONITORINFO { public int Size; public RECT Monitor,Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window,out RECT rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor,ref MONITORINFO info);
    public static void Attach(Forms.ContextMenuStrip menu)
    {
        menu.Opened+=(_,_)=> {
            Place(menu);
            // WinForms finishes popup activation after Opened. Reapply placement on
            // the next UI dispatch so its final positioning cannot cover the taskbar.
            menu.BeginInvoke(new Action(()=> { if(!menu.IsDisposed && menu.Visible) Place(menu); }));
        };
    }
    private static Rectangle WorkArea(IntPtr window)
    {
        var info=new MONITORINFO { Size=Marshal.SizeOf<MONITORINFO>() };
        if(!GetMonitorInfo(MonitorFromWindow(window,2),ref info)) throw new System.ComponentModel.Win32Exception();
        return Rectangle.FromLTRB(info.Work.Left,info.Work.Top,info.Work.Right,info.Work.Bottom);
    }
    internal static Point Position(Rectangle menu,Rectangle area)=>new(
        Math.Clamp(menu.X,area.Left,Math.Max(area.Left,area.Right-menu.Width)),
        Math.Clamp(menu.Y,area.Top,Math.Max(area.Top,area.Bottom-menu.Height)));
    private static void Place(Forms.ContextMenuStrip menu)
    {
        if(!GetWindowRect(menu.Handle,out var rect)) return;
        Rectangle area;
        try { area=WorkArea(menu.Handle); } catch(System.ComponentModel.Win32Exception) { return; }
        var point=Position(Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom),area);
        Native.SetWindowPos(menu.Handle,new IntPtr(-1),point.X,point.Y,0,0,0x0001 | 0x0040);
        Native.SetForegroundWindow(menu.Handle);
    }
    internal static void Verify()
    {
        foreach(var area in new[] { new Rectangle(0,0,1920,1040),new Rectangle(0,48,1920,1032),new Rectangle(-1840,0,1840,1080),new Rectangle(0,0,1840,1080) })
        {
            var menu=new Rectangle(area.Right-80,area.Bottom-20,280,180);
            var placed=new Rectangle(Position(menu,area),menu.Size);
            if(!area.Contains(placed)) throw new Exception("Tray menu overlaps taskbar work-area boundary");
        }
        using var popup=new Forms.ContextMenuStrip();
        for(int i=0;i<6;i++) popup.Items.Add("Tray placement fixture "+i);
        Attach(popup);
        var work=Forms.Screen.PrimaryScreen!.WorkingArea;
        popup.Show(new Point(work.Right-5,work.Bottom+10));
        Place(popup);
        if(!GetWindowRect(popup.Handle,out var bounds) || !WorkArea(popup.Handle).Contains(Rectangle.FromLTRB(bounds.Left,bounds.Top,bounds.Right,bounds.Bottom))) throw new Exception("Native tray popup is outside the work area");
        popup.Close();
    }
}
