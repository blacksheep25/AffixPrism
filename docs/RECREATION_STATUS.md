> Historical design/review notes. Some implementation status and instructions below are superseded by the root README and docs/FEATURE_AUDIT.md.

# POE Overlay II recreation status

Target: closely reproduce the useful POE Overlay II desktop workflows in Exile Lens, with no advertising, sponsored content or ad SDKs. User screenshots are the visual authority. The installed application remains a read-only reference; its program code, assets, identity and service credentials are not included in the Exile Lens build.

This target is not complete. Avoid describing incremental UI changes as a full recreation.

| Workflow | Exile Lens status | Remaining work |
| --- | --- | --- |
| Alt+E capture | Implemented; previously user-verified | Repeat capture after interacting with focused evaluator |
| Item card | Structured English parser; rarity colours; advanced metadata hidden | More item classes, languages and special modifier forms |
| Modifier inspection | Tiers, copied roll bounds and range positions; hybrid grouping metadata | Stat identity mapping; weighted modifier controls |
| Item filters | Item level/quality min and max, corruption, base and rarity for optional browser query | In-window modifier-driven search |
| Prices in window | Supported currency/unique economy estimates and variants | Current network access verification; seller search/fetch integration |
| Rare valuation | Not implemented | Independent authorized pricing integration with actual comparable offers |
| Comparison | Pin text and compare against next item | Structured property/modifier deltas |
| Guides | Expedition, Ritual, Delirium market categories; automatic Logbook guide | More verified triggers and contextual reward filters |
| Session | Local in-memory transitions and manual loot counts | Persistent history and richer analytics |
| Ads and sponsored UI | Absent | Keep absent |

## Pricing dependency

The installed reference has a search-then-fetch trade integration. Its presence on disk does not grant a new application service access. GGG's public developer documentation currently excludes internal endpoints and says new application registrations are unavailable (https://www.pathofexile.com/developer/docs). Exile Lens currently uses documented public poe.ninja economy data, which cannot provide modifier-based rare valuation. The current development host also rejects HTTPS socket access to that feed. No seller results, reliability scores or valuations may be invented to fill this gap.

## September 9 implementation

Advanced clipboard metadata is parsed into the item model instead of rendered as visible brace blocks. In-line value ranges are removed from display text and retained for tooltips. Hybrid modifier lines share metadata; multi-value modifiers do not get a misleading single roll percentage. A roll percentage represents position in its copied numeric range, not desirability or a price score. Rare headers are yellow, magic headers blue and unique headers orange. Copied text remains available in an expandable inspector. Level and quality filters accept minimum and maximum values and reject inverted ranges.

Validation: 58 core checks and WPF smoke tests, including the quarterstaff transcribed from the user's screenshot. No real market values are used in UI fixtures.

## Completion audit: remote dependency confirmed

Further read-only inspection of the installed main bundle found that its estimated-value feature sends item data to a remote prediction service on prediction.poeoverlay.com (with separate regional/bulk routing). The client receives predicted price, low/high estimates and confidence. The prediction model itself is not contained in the inspected client implementation. This is distinct from its cookie-authenticated trade-search and result-fetch integration.

A faithful recreation of that behavior therefore requires service access suitable for Exile Lens, or an independently implemented/trained pricing system and usable trade data. Copying the local visual client alone would not supply those capabilities. No request was made to the reference application's prediction service, no service credentials were extracted, and no remote service was represented as belonging to Exile Lens.

Completion remains blocked on these dependencies; the current application must not be described as a 1:1 recreation. This audit made no cosmetic changes or substituted sample results for live behavior.

## User scope update

Historical UI-stage status: pricing was deferred during the initial recreation. Live trade and economy requests are now implemented with cancellation, caching and request pacing. The evaluator supports editable filters, exact/broad presets, draft restoration and listing comparisons. Reference-wide parity and real-game/hardware validation remain unverified; this document does not establish full parity.
