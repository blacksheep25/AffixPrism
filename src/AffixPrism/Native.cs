using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;

namespace AffixPrism;
internal static class Native
{
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] internal static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] internal static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] internal static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public InputUnion data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT keyboard;
        [FieldOffset(0)] public MOUSEINPUT mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort key, scan; public uint flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int x, y; public uint data, flags, time; public UIntPtr extra; }
    internal static bool KeyDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    internal static IntPtr ForegroundGameWindow()
    {
        var hwnd = GetForegroundWindow();
        return IsGameWindow(hwnd) ? hwnd : IntPtr.Zero;
    }
    internal static bool IsGameWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            using var process = Process.GetProcessById((int)pid);
            var title = new StringBuilder(512); GetWindowText(hwnd, title, title.Capacity);
            return process.ProcessName.StartsWith("PathOfExile", StringComparison.OrdinalIgnoreCase) && title.ToString().Contains("Path of Exile 2", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or Win32Exception or InvalidOperationException) { return false; }
    }
    internal static bool SendCopyKey(ushort key, bool down)
    {
        var inputs = new[] { new INPUT { type = 1, data = new InputUnion { keyboard = new KEYBDINPUT { key = key, flags = down ? 0u : 2u } } } };
        return SendInput(1, inputs, Marshal.SizeOf<INPUT>()) == 1;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
    internal static bool GameIsForeground()
    {
        var title = new StringBuilder(512);
        GetWindowText(GetForegroundWindow(), title, title.Capacity);
        return title.ToString().Contains("Path of Exile 2", StringComparison.OrdinalIgnoreCase);
    }
}
