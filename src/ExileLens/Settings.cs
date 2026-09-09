using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ExileLens;
public sealed class WatchEntry
{
    public string Name { get; set; } = "";
    public decimal? Price { get; set; }
    public DateTimeOffset Updated { get; set; } = DateTimeOffset.UtcNow;
    public string ValueLabel => Price?.ToString("0.##") ?? "—";
    public string DateLabel => $"Manual · {Updated.ToLocalTime():dd MMM HH:mm}";
}
public sealed class Settings
{
    public string LogPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Path of Exile 2", "logs", "Client.txt");
    public string League { get; set; } = "";
    public string Currency { get; set; } = "Exalted Orb";
    public string PriceCheckHotkey { get; set; } = "Alt+E";
    public bool AutoShow { get; set; } = true;
    public bool Pinned { get; set; }
    public double Opacity { get; set; } = .96;
    public double Left { get; set; } = 80;
    public double Top { get; set; } = 80;
    public List<WatchEntry> Watchlist { get; set; } = new();
}
public static class SettingsStore
{
    public static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExileLens", "settings.json");
    public static Settings Load(out string? error)
    {
        error = null;
        try
        {
            if (!File.Exists(FilePath)) return new();
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? throw new JsonException("Empty settings");
            if (settings.Watchlist is null || settings.LogPath is null || settings.League is null || string.IsNullOrWhiteSpace(settings.Currency) || settings.Watchlist.Exists(x => x is null || string.IsNullOrWhiteSpace(x.Name) || x.Price < 0))
                throw new JsonException("Invalid settings values");
            settings.Opacity = double.IsFinite(settings.Opacity) ? Math.Clamp(settings.Opacity, .55, 1) : .96;
            settings.PriceCheckHotkey = ExileLens.Core.CheckHotkey.Parse(settings.PriceCheckHotkey)?.Label ?? "Alt+E";
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            error = "Settings could not be loaded. Defaults are in use; the original file is unchanged until you save.";
            return new();
        }
    }
    public static void Save(Settings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
}
