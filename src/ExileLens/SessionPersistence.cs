using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ExileLens.Core;
namespace ExileLens;
public partial class MainWindow
{
    private CopiedItem? comparisonBaseline;
    private TimeSpan previousSessionElapsed;
    private bool sessionCanSave = true;
    private sealed record SessionSnapshot(double ElapsedSeconds, int Checks, List<SessionArea> Areas, List<TrackedLoot> Loot, List<RecentCheck> Recent, CopiedItem? Baseline);
    private string SessionPath => smoke ? Path.GetFullPath("artifacts/smoke-session.json") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExileLens", "session.json");
    private void RestoreSession(bool forTest = false)
    {
        if ((smoke && !forTest) || !File.Exists(SessionPath)) return;
        try
        {
            if (new FileInfo(SessionPath).Length > 6 * 1024 * 1024) throw new JsonException("Session file too large");
            var state = JsonSerializer.Deserialize<SessionSnapshot>(File.ReadAllText(SessionPath)) ?? throw new JsonException("Empty session");
            if (!double.IsFinite(state.ElapsedSeconds) || state.ElapsedSeconds < 0 || state.ElapsedSeconds > 31536000 || state.Checks < 0 || state.Areas == null || state.Loot == null || state.Recent == null || state.Areas.Count > 200 || state.Loot.Count > 1000 || state.Recent.Count > 30 || state.Areas.Any(x => x == null) || state.Loot.Any(x => x == null || x.Quantity < 1 || string.IsNullOrWhiteSpace(x.Name)) || state.Recent.Any(x => x?.Item?.Details == null || ItemParser.Parse(x.Item.Details) == null)) throw new JsonException("Invalid session");
            previousSessionElapsed = TimeSpan.FromSeconds(state.ElapsedSeconds); sessionStarted = DateTimeOffset.Now; sessionChecks = state.Checks;
            areas.AddRange(state.Areas); loot.AddRange(state.Loot); recent.AddRange(state.Recent);
            AreaRows.ItemsSource = areas.ToArray(); LootRows.ItemsSource = loot.ToArray(); RecentItems.ItemsSource = recent.ToArray();
            comparisonBaseline = state.Baseline;
            if (comparisonBaseline != null) { ComparisonLeft.Text = comparisonBaseline.Details; MainComparisonLeft.Item = comparisonBaseline; }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            sessionCanSave = false;
            Notice.Text = "Could not load session history. Reset session to start a new saved history; the existing file is preserved.";
        }
    }
    private void SaveSession(bool forTest = false)
    {
        if ((smoke && !forTest) || !sessionCanSave) return;
        try
        {
            var state = new SessionSnapshot(Math.Min(31536000, (previousSessionElapsed + (DateTimeOffset.Now - sessionStarted)).TotalSeconds), sessionChecks, areas, loot, recent, comparisonBaseline);
            Directory.CreateDirectory(Path.GetDirectoryName(SessionPath)!);
            File.WriteAllText(SessionPath + ".tmp", JsonSerializer.Serialize(state));
            File.Move(SessionPath + ".tmp", SessionPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Notice.Text = "Session history could not be saved; it remains in memory."; }
    }
    private void VerifySessionPersistence()
    {
        int count = loot.Count, checks = sessionChecks;
        SaveSession(true);
        areas.Clear(); loot.Clear(); recent.Clear(); sessionChecks = 0;
        RestoreSession(true);
        if (!sessionCanSave || loot.Count != count || sessionChecks != checks) throw new Exception("Session round-trip failed");
        File.Delete(SessionPath);
    }

}
