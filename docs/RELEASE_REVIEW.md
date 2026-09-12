# Local release review — 13 September 2026

This is a local candidate, not a published release.

## User-facing changes

- Comparison cards clearly identify your item and the seller's item, highlight matching stats, and show numerical differences or missing modifiers directly on the cards.
- Similarity weights item-type suggested stats four times above secondary stats. Comparable rare/magic weapons can use different bases within the same weapon class; critical skill/arrow and DPS checks remain. Search filters still control which listings are fetched.
- Min/max fields stay visible; focusing a field no longer enables it. Editing its value does.
- Quiver, jewel, essence and wrapped usage text receive appropriate description styling instead of becoming modifiers.
- Exchange unit and stack prices share a currency, retain small values, and expose original price equivalents on hover. Stale rates never drive currency conversion.
- Local builds reuse artifacts/build. The manual cleanup script preserves that folder.

## Validation limits

Core regression tests and WPF smoke checks cover synthetic fixtures, search failures, request handling, filter layouts and comparison rendering. They do not prove real-market valuation accuracy, actual in-game shortcut behavior on every machine, or mixed-monitor correctness. No new network-dependent or clean-machine installation test is claimed by these checks. Settings remain outside the build in LocalAppData.

Use the manual checks in RELEASING.md before approving publication. Do not push or create a release without user approval.
