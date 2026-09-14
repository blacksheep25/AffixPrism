using System;
using System.IO;

namespace AffixPrism;
internal static class CaptureDiagnostics
{
    // Deliberately stores no item/clipboard text, names, chat or account data.
    public static void Record(string stage, long milliseconds)
    {
        try
        {
            string path = Path.Combine(Path.GetDirectoryName(SettingsStore.FilePath)!, "capture.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path) && new FileInfo(path).Length > 64 * 1024) File.Move(path, path + ".previous", true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} stage={stage} elapsedMs={milliseconds}\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
