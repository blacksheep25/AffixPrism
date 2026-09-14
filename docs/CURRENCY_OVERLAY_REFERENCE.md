> Historical design/review notes. Some implementation status and instructions below are superseded by the root README and docs/FEATURE_AUDIT.md.

# Currency overlay reference review

Reviewed 2026-09-09: https://github.com/POE2-VibeTools/poe2-currency-overlay (master; moving reference, not a pinned dependency).

The project supplies a useful example of live rare-item searches. It does not run its own rare-item market database: `trade2.js` posts a query to the Path of Exile trade website, then fetches listing details in batches of ten. It initially fetches one page and retains IDs for later pages. Requests use Electron's local session cookie jar. This is a website integration, distinct from GGG's published developer API. Reading this implementation does not verify connectivity from AffixPrism.

The strongest filter reference is its Exiled Exchange 2 parser/matching engine. Stable trade stat IDs and explicit pseudo-stat handling are needed for live query generation; AffixPrism's normalized clipboard text matching is currently only suitable for its imported comparable datasets. Do not silently translate an unsupported selected modifier into an omitted live filter. Show unsupported mappings before searching.

Useful implementation requirements:

- Keep search and listing-fetch rate policies separate; learn limits from response headers and honor Retry-After and reported cooldowns.
- Fetch the first ten listings, then request additional pages on demand.
- Preserve English query identifiers independently from localized listing text.
- Bind any account sign-in to AffixPrism's own local browser session; never read another overlay's cookies.
- Treat pseudo resistance totals, weapon DPS and multiple numeric modifier values as explicit query semantics, not interchangeable text signatures.

Licensing: the application is GPL-3.0; its vendored EE2 parser carries a separate MIT notice. No source code or assets were copied into AffixPrism during this review. Before vendoring parser code, pin the upstream revision, inspect its resource/dependency notices and include the applicable licence and attribution.

Sources:
- https://github.com/POE2-VibeTools/poe2-currency-overlay/blob/master/trade2.js
- https://github.com/POE2-VibeTools/poe2-currency-overlay/blob/master/README.md
- https://github.com/POE2-VibeTools/poe2-currency-overlay/blob/master/LICENSE
- https://github.com/POE2-VibeTools/poe2-currency-overlay/blob/master/renderer/vendor/ee2/LICENSE

Status: review only. Live rare-item collection remains unconnected in AffixPrism. This reference identifies a practical website-search adapter and stat-mapping approach, not a completed live integration.
