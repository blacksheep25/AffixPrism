using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace AffixPrism.Core;
public sealed class MarketServiceClient(HttpClient http)
{
    public async Task ImportAsync(string json, CancellationToken token)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/listings/import") { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        message.Headers.Add("X-AffixPrism-Client", "desktop");
        using var response = await http.SendAsync(message, token);
        await Check(response, token);
    }
    public async Task<ComparableResult?> SearchAsync(ComparableRequest request, CancellationToken token)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/price-check") { Content = JsonContent.Create(request) };
        message.Headers.Add("X-AffixPrism-Client", "desktop");
        using var response = await http.SendAsync(message, token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await Check(response, token);
        return await response.Content.ReadFromJsonAsync<ComparableResult>(cancellationToken: token) ?? throw new JsonException("Empty price response");
    }
    private static async Task Check(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        string message = $"Market service returned {(int)response.StatusCode}.";
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (json.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String) message = error.GetString()!;
        }
        catch (JsonException) { }
        throw new HttpRequestException(message);
    }
}
