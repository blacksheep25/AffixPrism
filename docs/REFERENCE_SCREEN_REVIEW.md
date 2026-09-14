# Reference screen review — 12 September 2026

Reviewed all seven user-supplied screenshots under the local, excluded `references/POE2_Overlay_II` folder. Screenshots and account names are not published in the repository. This is a reconstruction inventory, not a claim that these screens are implemented.

| Screen | Observed layout and behaviour | Reconstruction scope |
| --- | --- | --- |
| Overview | Left navigation, character banner, time breakdown, mapping metrics/history, stash summary and market movers | Reuse logged area/session data; character XP, gold and stash need a verified source before displaying real figures |
| Market Prices | Category navigation, search, prices, stock, history sparklines and favourites | A searchable market browser can build on the corrected economy parser; stock/history must come from actual source fields |
| Item Search and Compare | Nearby paired cards, stat grouping, tiers, aggregate stats and listing actions | Continue refining existing evaluator/comparison; do not invent scoring or hidden stat metadata |
| Character Campaign | World/town/trial/hideout time breakdown and per-act bars | Requires reliable area-to-act mapping and persisted time segments |
| Character Leveling | Current level progress and XP/time curve | Requires verified character experience snapshots; Client.txt alone does not supply these values |
| Character Mapping | Map summary and history with deaths, XP and gold | Map entry timing is available; completion, XP and gold must not be inferred from mere area departures |
| Trade History | Earned/spent/net cards, currency/time/search filters and grouped trade rows | Needs a confirmed transaction source; whispers or listed prices do not establish completed trades |

The local UI revision implements left sidebar navigation, dark framed content panels, serif page headings, and a wider default window. Item check and comparison now use the shared coloured item cards. Session overview, recorded loot and area history have separate panels. Navigation scrolls at small window sizes. This presents the existing supported features; character XP, stash tracking and confirmed trade history remain dependent on verified data sources.

Evaluation descriptions, flavour text and quest instructions now belong inside the bordered item card. Search presets and active filters follow outside that card, with numeric controls alongside it. Non-equipment inspections hide the active-filter controls entirely. Local renders are under `artifacts/showcase/main-*.png` and `artifacts/showcase/quest-item.png`.

Publishing policy: obtain the user's approval before any GitHub push or release. This UI revision is local and unpublished.

The follow-up local revision uses a neutral charcoal main workspace, removes Compare from navigation, keeps the main window opaque and non-topmost, and stacks item actions at narrow widths. The Prices page searches market categories through the existing economy client, refreshes every five minutes only while visible, and displays source timestamps and stale-cache status. A direct live connection check failed with Windows socket access denied; fresh prices have not been verified on this host. Demo captures are explicitly labelled and are not production data.

The evaluator now groups active-filter controls and bounds in one side panel. Only selected stats show bounds by default; Show all stats exposes the rest. Broad tolerance leaves item level, requirements, quality, skill levels, discrete counts and negative values unchanged; users may edit these manually. Performance rolls retain tolerance. Regression checks cover fixed bounds, selection visibility, window behaviour and market name search.

## Reference-led reconstruction (local)

The subsequent revision replaces the earlier custom workspace layout: a 30px title bar, 148px left navigation with Settings at the bottom, and a centred 1088px content column now follow the supplied reference proportions. Home is the default destination, with a banner, session time strip, metric tiles, area-history rows and saved market favourites. These use recorded session data; no character XP, completed-map or stash figures are invented.

Market Prices uses the reference's separate category rail, search above compact rows, item artwork, price, provider listing count, history sparklines and favourite actions. Relative official artwork paths and both provider sparkline spellings are supported. Missing points remain gaps, and unavailable listing counts remain dashes. The narrow layout hides the history column to preserve readable prices. Stock is not inferred from trading volume.

The evaluator uses a slim title bar, subdued patterned nameplate and a compact card-to-price layout. Secondary search controls collapse, while bookmark/pin actions remain in the nameplate menu. Comparison cards have matching thin ornamental nameplates and smaller artwork. Original AffixPrism branding remains; the reference's proprietary background and character portrait assets are not included.

Reviewed renders: `artifacts/showcase/reference-home.png`, `market-prices.png`, `price-check.png` and `comparison.png`. Market screenshots contain explicitly labelled isolated demo rows; production uses the economy provider. The known Windows network block remains a separate live-data limitation.

## Default-filter comparison

Reviewed `renderer/src/web/price-check/filters/create-stat-filters.ts` in the retained Exiled Exchange 2 snapshot. `calculatedStatToFilter` defaults to disabled; specific item categories and the explicit `defaultAllSelected` option enable filters. AffixPrism previously auto-selected up to three exact numerical requirements, which could make the first search too restrictive.

Beta 3 starts ordinary searches with base/rarity/state constraints and no numerical modifiers. **Suggested filters** applies our category heuristic explicitly. **Broad search** clears numerical selections. Active saved profiles and per-item drafts remain deliberate overrides. Broad asking prices are still not presented as a reliable item valuation without sufficient similar peers.
