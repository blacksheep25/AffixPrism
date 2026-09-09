# ExileLens

A compact, ad-free Windows companion for **Path of Exile 2**. Check an item with **Alt+E**, inspect comparable asking prices, tune filters, and keep useful information beside the game.

**Status: experimental.** Equipment search, economy prices and rune recognition are implemented, but external services can reject requests and item matching is still evolving. This is not a complete replacement for every established overlay.

![Item evaluation with separate filters and embedded listings](docs/screenshots/price-check.png)

*Actual application controls rendered with synthetic items/listings. All showcase prices and sellers are demonstration data, not live market quotes.*

## Features

- **In-window price checks:** configurable capture shortcut, official trade-site search, listings, seller comparisons, and supported economy/exchange prices.
- **Useful filters:** item-type defaults, selectable base type, crafting-base preset, min/max bounds, broad matching, common pseudo stats, reusable profiles and undo.
- **Comparable estimates:** currency conversion when fresh rates are available; a recommendation requires sufficiently similar items from at least three independent sellers.
- **Item cards:** rarity and modifier styling, DPS, flavour text, corruption states, artwork and socket tooltips when the source supplies enough information.
- **Keep items handy:** bookmarks, notes, recent-check navigation, comparison windows and separate screen pins.
- **Rune choice helper:** select a menu region, run local English OCR, and display indicative prices beside recognised choices while POE2 is foreground.
- **Guides and session tools:** automatic Logbook guide activation, manual encounter guides/shortlist, session history and manual loot tally.
- **Desktop conveniences:** draggable panels, remembered placement, opacity, click-through, tray controls and dismissal when clicking outside evaluation.

Read the [feature audit](docs/FEATURE_AUDIT.md) for scope and limitations, or browse the [screenshot gallery](docs/SCREENSHOTS.md).

## Run from source

Requires Windows 10/11 x64, the **.NET 8 SDK**, and the Microsoft Visual C++ 2015–2022 x64 runtime for native OCR. Use POE2 in windowed or borderless mode. Linux/macOS are not supported.

```powershell
git clone https://github.com/blacksheep25/ExileLens.git
cd ExileLens
dotnet run --project src/ExileLens -c Release
```

Alternatively, run `Start Exile Lens.cmd`. Select your league in Settings and browse to your installation's `logs/Client.txt` for area detection. Hover an item in-game and press Alt+E. Ctrl+Alt+O toggles the management panel; Ctrl+Alt+L toggles click-through. The tray menu restores hidden panels.

For rune prices, open **Guides → Rune choice helper**, select the choice-name region, then start live prices. Region selection and activation are manual; recognition runs locally.

## Build and validate

```powershell
dotnet run --project tests/ExileLens.Tests -c Release
dotnet publish src/ExileLens -c Release -o artifacts/publish
dotnet run --project src/ExileLens -c Release -- --smoke-test
```

Run from the repository root. UI smoke tests require a Windows desktop, generate screenshots under `artifacts/`, and report to `artifacts/smoke-result.txt`. They use isolated fixture data; they do not prove live service availability. The bundled English OCR model allows recognition without downloading a model at runtime.

## Data, privacy and limitations

Settings, copied item history, bookmarks, profiles, caches and imported datasets are stored locally under `%LOCALAPPDATA%\ExileLens`. Do not include that directory in bug reports. Rune captures are processed in memory and are not saved or uploaded by normal operation.

Trade queries send selected item/filter information to Path of Exile's trade website. Economy data comes from poe.ninja; artwork can load from official item-image hosts. The website trade endpoints are not a guaranteed public developer API. Rate limits, verification challenges and network failures can prevent results; the app respects cooldowns and does not bypass verification.

Prices are **asking prices**, not completed sales. Similarity is a heuristic over a limited sample, not a promise of an item's selling price. Unknown sockets, missing rates, unrecognised modifiers and unsupported categories can limit results. English item text only. There is no automatic ground-loot pickup/value tracking, automated trading, or complete feature parity claim.

The optional `src/ExileLens.MarketService` is a loopback-only imported-data service, not a live collector. The desktop app does not require it. Do not expose it publicly; its custom header is not authentication.

## Project layout

- `src/ExileLens`: WPF overlay, capture, windows, OCR and local persistence.
- `src/ExileLens.Core`: item parsing, filters, market adapters, pricing and matching.
- `src/ExileLens.MarketService`: optional local import service.
- `tests`: executable core checks and item fixtures.
- `docs`: current audit, guides and historical design notes.

## License and acknowledgements

Original ExileLens code is [MIT licensed](LICENSE). Third-party materials retain their licenses; see [THIRD_PARTY.md](THIRD_PARTY.md). Inspired by Exiled Exchange 2, RuneHelper and established overlay workflows. ExileLens is an independent community project, not affiliated with Grinding Gear Games or those projects. Path of Exile names and game content belong to their respective owners.
