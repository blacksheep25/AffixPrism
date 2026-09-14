using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using AffixPrism.Core;

namespace AffixPrism;
internal sealed class LeagueService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(8), MaxResponseContentBufferSize = 256 * 1024 };
    private static string CachePath => Path.Combine(Path.GetDirectoryName(SettingsStore.FilePath)!, "leagues.json");
    private DateTimeOffset lastAttempt;
    private (IReadOnlyList<LeagueOption> Items, string Status)? previous;
    public async Task<(IReadOnlyList<LeagueOption> Items, string Status)> LoadAsync(CancellationToken token)
    {
        // At most one request per five minutes, including failures/refresh clicks.
        if (previous != null && DateTimeOffset.UtcNow - lastAttempt < TimeSpan.FromMinutes(5)) return previous.Value;
        IReadOnlyList<LeagueOption> fallback = Leagues.Bundled;
        string fallbackLabel = "Offline list · verified 9 Sep 2026";
        try
        {
            if (File.Exists(CachePath))
            {
                fallback = Leagues.Parse(await File.ReadAllTextAsync(CachePath, token));
                var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(CachePath);
                fallbackLabel = "Saved league list · poe.ninja";
                if (age >= TimeSpan.Zero && age < TimeSpan.FromHours(6)) return (fallback, fallbackLabel);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        lastAttempt = DateTimeOffset.UtcNow;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://poe.ninja/poe2/api/economy/leagues");
            request.Headers.UserAgent.ParseAdd("AffixPrism/0.1 (personal-development)");
            using var response = await Client.SendAsync(request, token);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(token);
            var items = Leagues.Parse(json);
            try { Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!); await File.WriteAllTextAsync(CachePath, json, token); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            previous = (items, "League list updated · poe.ninja");
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { previous = (fallback, fallbackLabel + " · refresh timed out"); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException) { previous = (fallback, fallbackLabel + " · refresh unavailable"); }
        return previous!.Value;
    }
}
