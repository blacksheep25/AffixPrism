> Historical design/review notes. Some implementation status and instructions below are superseded by the root README and docs/FEATURE_AUDIT.md.

# PoE Overlay II installed-file review

Inspected 9 September 2026. Source installation: `%LOCALAPPDATA%\Programs\PoE Overlay II Standalone`.

## Scope and evidence

Read-only inspection of the installed ASAR archive and plugin filenames/metadata strings. The package identifies itself as PoE Overlay II Standalone **1.64.1**, Electron **42.7.1**, by Kyusung Interactive Co., Ltd. Its package license field is `UNLICENSED`. No application code was executed, installation files changed, account/session data read, or remote trade requests made in this review. Exile Lens implementation was not changed by this review.

Selected bundles were extracted locally under ignored `artifacts/poe-overlay-research` for searching. They are reference material, not Exile Lens source or distributable assets. Findings below are paraphrases of observed behavior.

## Item capture

The `copyItem` routine in `resources/app.asar` → `dist/main/main.js`:

1. Rejects an overlapping operation with a busy result.
2. Sends Ctrl+C through its input-system abstraction and native WinAPI plugin.
3. Waits **115 ms after the input call completes**.
4. Reads clipboard text, then initiates clearing the clipboard.
5. Returns an empty result if no text exists; otherwise passes the text through an ordered parser pipeline.
6. Awaits clipboard clearing before returning the parsed item or parser-error result.
7. Releases its busy flag even on exceptions.

The visible routine does not require clipboard owner PID equality or clipboard sequence-number changes. It clears the clipboard after reading instead. That does not establish that all lower-level clipboard code lacks additional checks.

## Input and window handling

The input system wraps simulated keystrokes in `skipKeyboardEvents`:

- It temporarily sets designated overlay windows to keyboard passthrough.
- On the first transition into that state, it waits **50 ms** before running the input action.
- It schedules restoration of keyboard handling **500 ms after** the action completes; subsequent actions can extend this interval.
- With one modifier, Ctrl+C is dispatched through `simulateModifiedKeyStroke1`.

The installed native helper is `resources/resources/plugins/KyusungInteractive.Overlay.WinAPI.dll`. It is hosted alongside `KyusungInteractive.Overlay.Host.exe` and `WindowsInput.dll`. DLL metadata strings reference WindowsInput and clipboard functions. **The exact native key-down/key-up timing was not established** by this inspection; the 115 ms delay is a post-input delay, not evidence of how long C is held.

There is also a modifier-release helper in the input abstraction, but the observed `copyItem` body does not invoke it directly. Other features do. Do not assume every hotkey path has identical release handling.

## Evaluation and prices

The evaluate-hotkey handler clears previous evaluation state, requests an item copy, handles explicit failure codes, detects the item's language, enriches the parsed item, then opens the evaluation window.

The application includes:

- An ordered item-parser pipeline and stat-description matching.
- Item/stat metadata and language handling.
- Evaluation configuration for profiles, weighted modifiers, price currency, listing age and seller status.
- An HTTP trade search client and a separate result-fetch step that maps listing prices/currencies into application data.
- An evaluation renderer with fetched/total counts, listing results and loading/failure states.
- Trade rate-limit header processing, including rule state and retry-after timestamps.

This is substantially more than opening a trade URL. The installed client's existence does not establish credentials, service access, or a supported integration for a separate application; this review did not contact or test those services.

## Leagues and overlay windows

Its trade-data service fetches league records from its trade-data client, stores league identifiers and display text, and selects the first returned league when none is configured. This differs from Exile Lens's currently selected public economy-list source and explicit user selection.

The evaluation, guide and inspection panels are declared separately. The evaluation window requests keyboard focus; guide and inspection windows are configured to ignore keyboard events. Consequently, copying their focus behavior alone would miss the associated input-passthrough handling.

## Comparison with Exile Lens

| Concern | Installed PoE Overlay II | Current Exile Lens |
| --- | --- | --- |
| Copy concurrency | Busy flag | Busy flag |
| Keyboard routing | Temporary overlay passthrough around simulated input | Foreground-game check and nonactivating capture-result window |
| Copy timing | Native modified key stroke, then 115 ms wait | 35 ms modifier settling, 90 ms C hold, then fresh-clipboard polling |
| Clipboard freshness | Read then clear in visible copy routine | Changed sequence plus valid item text; clipboard retained |
| Item understanding | Multi-stage parser, stat metadata and language handling | Basic English item header parsing |
| Prices | Search, fetch and display listings within evaluation UI | Generated browser search; no in-overlay listings |
| League choices | Its trade-data service | Public economy feed with cached/bundled fallback |

## Historical recommendations

These were recommendations from the initial review, not the current implementation backlog. Input routing, asynchronous pricing, cancellation, caching and request pacing now exist. Parsing coverage and real-game verification remain ongoing; see FEATURE_AUDIT.md and RELEASING.md for current limitations.

1. Build an Exile Lens input-routing scope that handles its own focused windows explicitly, restores state on every exit path, and does not send inputs to unrelated applications.
2. Keep the existing freshness check and diagnostics; there is no need to clear the user's clipboard merely to imitate the other application.
3. Expand item parsing and modifier-to-search-filter mapping using an independently implemented data model and an appropriate data source.
4. Establish the supported pricing integration, then add asynchronous search/results, cancellation, caching and rate-limit handling inside Exile Lens.

The most useful lesson is the separation of input routing, item parsing and price retrieval—not simply choosing a different fixed delay.

## Locators for reinspection

Offsets below are zero-based character offsets in the decoded installed minified bundle and are version-specific. They are not source line numbers.

| Bundle inside app.asar | Locator / approximate offset |
| --- | --- |
| `package.json` | Product identity and version |
| `dist/main/main.js` | `copyItem`, 1,048,068 |
| `dist/main/main.js` | `skipKeyboardEvents`, 543,243 |
| `dist/main/main.js` | Input-system `sendKey`, 555,615 |
| `dist/main/main.js` | WinAPI wrapper, 530,282 |
| `dist/main/main.js` | Parser pipeline `Rp`, 1,040,941 |
| `dist/main/main.js` | Evaluate-hotkey handler, 1,134,453 |
| `dist/main/main.js` | League data service, 843,082 |
| `dist/main/main.js` | Trade search, 851,379; fetch, 853,944 |
| `dist/main/main.js` | Trade rate-limit parser, 689,485 |
| `dist/main/main.js` | Evaluation/guide window declarations, 1,259,180 |
| `dist/renderer/ingame-evaluate.js` | Listing-result renderer and search configuration |
