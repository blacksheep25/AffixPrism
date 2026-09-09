# Feature audit

Reviewed 9 September 2026 for the first public source publication. This is a source, automated-test and rendered-UI audit; it is not certification of uninterrupted live trade access or exhaustive item coverage. Earlier review documents are retained as historical notes.

| Area | Implemented | Practical limits / follow-up |
| --- | --- | --- |
| Item capture | Configurable Alt+E, foreground-game checks, clipboard change detection, cancellation and copy diagnostics | English text; elevated game permissions and exclusive fullscreen can interfere |
| Item parsing | Rare/magic/unique/gem/currency, known magic bases, unidentified state, typed modifiers and common pseudo stats | Heuristics and catalogue coverage cannot identify every future item; unknown bases fail explicitly |
| Equipment search | Trade2 search/fetch, stat catalogue mapping, base/category, rarity, identification, corruption, price currency, seller and age constraints | Website endpoints can change or require verification; no embedded account authentication |
| Filter editing | Type-specific defaults, base-name toggle, crafting-base preset, broad/item-value bounds, discrete skill bounds, multi-value fields | Unsupported/ambiguous selected stats stop search; some multi-value validation runs locally on fetched offers |
| Rate controls | Endpoint throttles, server cooldown headers, cancellation, short duplicate-query cache, bounded fetch batches | Shared IP/account limits can still be reached; there is no bypass or automatic retry loop |
| Listing display | Embedded asking prices, stock when supplied, seller rows, official item icons, comparison actions | A limited fetched sample, not the entire market; missing fetched matches do not prove no market exists |
| Price estimate | Similarity gates, critical modifiers, DPS checks, independent sellers, median and synthetic average comparison | Requires at least three sellers with sufficient similarity; not a completed-sale model or guaranteed sell price |
| Currency handling | Auto equipment currency search; conversion using fresh economy rates; supported category ratios and stack values | Missing/stale rates prevent conversion; unsupported identities/categories remain unavailable |
| Item presentation | Separate filter panel; adaptive height; rarity, crafted/enchant/desecrated, corruption, lore and gem styling; DPS | Oversized cards still need scrolling on small monitors; metadata/text heuristics need wider regression fixtures |
| Sockets/artwork | Fetched socket data and safe official icon URLs; copied-effect inference; tooltips | Copied text may omit exact contents/order. Unknowns remain labelled and images can fail to load |
| Comparisons | Side-by-side normalised cards, numerical changes collapsed by default, average-item comparison | Missing source stats are not silently invented; a positive numerical delta is not necessarily an upgrade |
| Bookmarks/pins | Local bookmarks/notes, recent checks, independent pinned cards | Screen pins are temporary; not restored as a full workspace after restart |
| Filter profiles | Save selected stat signatures by item class, reuse current-item bounds, active-filter summary, undo | Not a weighted-query editor. Persistence failure feedback needs further hardening |
| Map warnings | User-defined literal phrases highlighted when a waystone is copied | No automatic background map inspection or built-in exhaustive danger-rule catalogue |
| Rune choices | User-selected region, local Tesseract English OCR, confidence/ambiguity guards, cached indicative price annotations | Requires setup/start; only scans with POE2 foreground; approximate names require review; not rune auto-selection |
| Area guides | Structured Client.txt events; separate Logbook guide, watchlist, manually selected encounter guides | Ritual/Delirium context is not automatically inferred without verified events |
| Session | Time, recent checks, area history, manual loot tally | Automatic ground-loot detection/value tracking was removed; no zero-input loot accounting |
| Market tools | Candidate views and user-input resale/corruption outcome calculator | No automated trading, reliable outcome odds, live arbitrage engine or profit guarantees |
| Desktop usability | Dragging, opacity, saved placement, monitor clamping, click-through, tray popup work-area/topmost handling, outside-click evaluation dismissal | Tray placement checked with native popup smoke test; broader multi-monitor/DPI/taskbar testing remains valuable |
| Optional service | Loopback imported-dataset storage/query, size bounds and browser-origin rejection | Custom header is not authentication; never expose the service remotely; not a live data collector |

## Validation and publication fixes

- **201 core checks pass**, covering parsing, capture state, filter/query generation, mock HTTP search/fetch shapes, rate handling, currencies and OCR name matching.
- **Windows UI smoke passes**, including filter workflows, layout, profiles, comparisons, placement, tray popup bounds and native OCR against a generated text image.
- Desktop publishing succeeds. CI builds the desktop and optional service and runs core checks. UI smoke remains a desktop check.
- Replaced a developer-specific default game path with the conventional Steam path; other installations select their log in Settings.
- Removed the smoke test dependency on an external reference checkout; it now generates its OCR fixture locally.
- Excluded build artifacts, local reference clones, tool-specific state and environment/key files. No personal saved datasets or settings are part of this repository.
- Replaced contradictory accumulated README status notes with a current overview and labelled historical documents.
- Retained dependency licenses and added the project MIT license. Showcase captures contain synthetic sellers/prices and no desktop/chat background.

## Recommended next work

1. Expand real, sanitised item fixtures for uncommon modifier kinds, sockets and currencies.
2. Add deterministic HTTP fixtures for more server errors and catalogue revisions; expose clearer sample-size/coverage diagnostics.
3. Harden profile-save error reporting and add broader multi-monitor/DPI/manual game integration checks.
4. Improve known-socket artwork resolution without claiming identification from ambiguous clipboard text.
5. Evaluate similarity recommendations against a labelled benchmark before claiming valuation accuracy.

The implemented feature set is useful, but full parity with POE Overlay II or Exiled Exchange 2 has **not** been established.
