> Historical design/review notes. Some implementation status and instructions below are superseded by the root README and docs/FEATURE_AUDIT.md.

# Exiled Exchange 2 search review

Reference: https://github.com/Kvan7/Exiled-Exchange-2 at cca30662bf31eaf38bd711e2ec1a6b899a06c40e. Reviewed 2026-09-09. MIT licence retained in src/ExileLens/ThirdParty/Exiled-Exchange-2-LICENSE.txt. ExileLens changes are independently written.

Implemented: base/category search, numeric modifier filters, seller status/age, mixed listing currencies, comparison previews. This pass adds physical/elemental/total DPS from displayed values, server equipment filters for DPS/attack rate/critical chance, and up to 30 offers fetched in batches of ten with existing rate limits.

Remaining gaps: aggregate pseudo stats (resistances/attributes), quality-normalised DPS, augment editor, special unique presets, complete gem variants, live bulk offers/minimum stock, load more, seller collapse, trade-query link, multilingual parsing. Current defaults are a basic heuristic, not equivalent presets. Ordinary cut-gem pricing remains unsupported.

Reference files reviewed: renderer/src/web/price-check/filters/create-presets.ts, create-item-filters.ts, pseudo/item-property.ts, and trade/pathofexile-trade.ts.

This is a first improvement pass, not full parity. Prices remain asking prices from a limited sample, not completed sales.

Follow-up audit: added server seller collapsing, a 30-second identical-query cache, exact trade-query links, found/fetched/shown counts, and a calculated total-elemental-resistance filter. Other pseudo totals and specialised presets remain incomplete. Validated with 168 checks and WPF smoke tests; live-market equivalence has not been established.
