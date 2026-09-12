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

Visual direction: compact left navigation, dark panels, restrained gold outlines, clear tab headings, readable tables, and no advertisement column. The broader dashboard reconstruction is separate from beta 3's item-routing fixes.

## Default-filter comparison

Reviewed `renderer/src/web/price-check/filters/create-stat-filters.ts` in the retained Exiled Exchange 2 snapshot. `calculatedStatToFilter` defaults to disabled; specific item categories and the explicit `defaultAllSelected` option enable filters. ExileLens previously auto-selected up to three exact numerical requirements, which could make the first search too restrictive.

Beta 3 starts ordinary searches with base/rarity/state constraints and no numerical modifiers. **Suggested filters** applies our category heuristic explicitly. **Broad search** clears numerical selections. Active saved profiles and per-item drafts remain deliberate overrides. Broad asking prices are still not presented as a reliable item valuation without sufficient similar peers.
