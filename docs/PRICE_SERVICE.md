> Historical design/review notes. Some implementation status and instructions below are superseded by the root README and docs/FEATURE_AUDIT.md.

# Exile Lens price service

The application now has its own local listing service and comparable-price engine. This is not POE Overlay's remote prediction model. The service currently ingests supplied listing datasets; it does not yet collect live rare-item offers. No trade account cookies or third-party application credentials are used.

## Run

1. Run `artifacts/pricing/market-service/ExileLens.MarketService.exe`. Keep its console open; Ctrl+C stops it.
2. Exit older overlay instances and run `artifacts/pricing/ExileLens.exe`.
3. Select your league in Settings, inspect an item, and use **Import listings** in the evaluator.
4. Select the modifiers and bounds you want, choose currency/status/age, and click **Check prices**. Alt+E automatically checks against the loaded league dataset on subsequent captures.

The service listens only on localhost port 47921. Imported datasets persist under `%LOCALAPPDATA%\ExileLens\market-service`, one file per league. An import replaces that league's dataset. Test runs can isolate storage with `--data-dir <directory>`. The packaged service requires the .NET 8 ASP.NET runtime or SDK.

## How estimates work

Matches require the same league, base type, rarity, corruption and identification state. Unique names must also match. Selected filters compare normalized English line text and the first numeric value on that line. Multi-value damage lines currently constrain only their first value; stat-ID mapping, pseudo stats, multi-value constraints and semantic equivalence are future work. No selected filters means base/rarity comparison only, explicitly labelled in the UI.

Currency values are never mixed or silently converted. Seller status and listing age are applied from the dataset. The estimate uses the cheapest matching listing from each distinct seller. With at least three sellers, it shows the median and interpolated 25th–75th percentile range. With fewer sellers, it displays listings without an estimate. These are asking prices, not sale prices or a confidence-rated prediction. Manipulation and stale listings remain possible; the capture timestamp and source are visible.

The overlay falls back to the documented poe.ninja economy feed for supported currency/unique categories when no league dataset is available. Live delivery of that external feed is still unverified on this host. It cannot replace rare-item comparable data.

## Listing format

UTF-8 JSON, maximum 10 MB / 10,000 listings. Capture time must be within 30 days and cannot be in the future. IDs must be unique. Prices must be positive. Item text must be supported English clipboard text.

```json
{
  "league": "Standard",
  "source": "Your listing source",
  "capturedAt": "2026-09-09T00:00:00Z",
  "listings": [
    {
      "id": "unique-listing-id",
      "account": "seller-name",
      "price": { "amount": 10, "currency": "Exalted Orb" },
      "itemText": "Item Class: Rings\nRarity: Rare\nExample Ring\nGold Ring\n--------\nItem Level: 80\n+100 to maximum Life",
      "listedAt": "2026-09-08T23:00:00Z",
      "online": true,
      "instantBuy": true
    }
  ]
}
```

The example is illustrative, not market data. Replace all fields with actual supplied listings.

## API

- `GET /health`: service identity and collection capability.
- `GET /api/leagues`: leagues with loaded datasets.
- `POST /api/listings/import`: body is a listing document as above.
- `POST /api/price-check`: body is the serialized `ComparableRequest` from ExileLens.Core: item (with clipboard `details`), league, currency, filters (`text`, `minimum`, `maximum`), onlineOnly, instantBuyOnly, maximumAgeDays.

POST requests require `X-ExileLens-Client: desktop`. Browser-origin requests are rejected, CORS is disabled, and the listener is loopback only. These are local-process protections, not multi-user authentication; remote hosting would require authentication and transport security.

## Validation and next source adapter

71 core checks pass. A real local HTTP integration test validated import, query serialization, the expected median and rejection of writes without the client header. WPF smoke tests render labelled synthetic results separately from real user data.

The next source adapter must populate this dataset format from an appropriate source of rare-item offers. Creating the service does not by itself create a live supply of trade data. GGG's published developer API and poe.ninja's published economy surface do not currently provide the rare-listing feed needed here. References: https://www.pathofexile.com/developer/docs and https://poe.ninja/docs/api.
