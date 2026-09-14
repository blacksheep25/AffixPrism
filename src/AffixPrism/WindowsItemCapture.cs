using System;
using System.Runtime.InteropServices;
using AffixPrism.Core;
using System.Threading;
using System.Threading.Tasks;

namespace AffixPrism;
internal sealed class WindowsItemCapture(Func<CheckHotkey> shortcut) : IItemCapturePlatform
{
    public nint ForegroundGame => Native.ForegroundGameWindow();
    public bool ShortcutKeysReleased => !Native.KeyDown((int)shortcut().Key) && !Native.KeyDown(0x10) && !Native.KeyDown(0x11) && !Native.KeyDown(0x12) && !Native.KeyDown(0x5B) && !Native.KeyDown(0x5C);
    public uint ClipboardVersion => Native.GetClipboardSequenceNumber();
    public Task<bool> SendCopyAsync(CancellationToken cancellationToken) => CopyChord.SendAsync(new GameKeySender(Native.ForegroundGameWindow()), cancellationToken);
    public string? ReadItemText(nint gameWindow)
    {
        // Clipboard ownership may be null or reassigned by clipboard utilities.
        // Freshness and unchanged game focus are enforced by ItemCapture; validate
        // the item text itself rather than rejecting a valid copy by owner PID.
        try { return System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : ""; }
        catch (ExternalException) { return null; }
    }
}

internal sealed class GameKeySender(nint game) : ICopyKeySender
{
    public bool HasGameFocus => game != 0 && Native.ForegroundGameWindow() == game;
    public bool SendKey(ushort virtualKey, bool down) => Native.SendCopyKey(virtualKey, down);
}
