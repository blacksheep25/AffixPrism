using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
namespace AffixPrism;
internal sealed class OutsideClickDismiss : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X,Y; }
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle,out uint process);
    private readonly DispatcherTimer timer;
    private bool wasDown;
    private static bool Down() => (GetAsyncKeyState(1)&0x8000)!=0 || (GetAsyncKeyState(2)&0x8000)!=0 || (GetAsyncKeyState(4)&0x8000)!=0;
    public OutsideClickDismiss(Window window)
    {
        wasDown=Down();
        timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_,_) => {
            bool down=Down();
            if(down && !wasDown && GetCursorPos(out var point))
            {
                GetWindowThreadProcessId(WindowFromPoint(point),out var process);
                // Keep dropdowns, comparison and other AffixPrism controls usable.
                if(process != Environment.ProcessId && window.IsVisible) window.Hide();
            }
            wasDown=down;
        };
        timer.Start();
    }
    public void Dispose() => timer.Stop();
}
