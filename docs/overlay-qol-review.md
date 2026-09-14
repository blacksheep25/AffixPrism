> Historical design/review notes. Some implementation status and instructions below are superseded by the root README and docs/FEATURE_AUDIT.md.

# POE overlay features and usability review

Reviewed 2026-09-09 against official documentation and the checked-out Exiled Exchange 2 source.

Sources:
- https://github.com/SnosMe/awakened-poe-trade/blob/master/docs/quick-start.md
- https://github.com/Kvan7/Exiled-Exchange-2
- https://www.poeoverlay.com/faq

## Available in AffixPrism

Hotkey item capture; editable stat bounds; exact-base/category filters; selected defaults; physical/elemental/total DPS; total elemental resistance pseudo filter; mixed-currency asking-price comparisons with available recent rates; seller status and age; deduplicated sellers; exact trade query link; bookmarks; pinning; item comparisons; remembered placement; opacity; escape dismissal; session and area tools.

This update: clicking outside AffixPrism dismisses the transient price checker, preserves pins and cancels pending lookups. Clicks on other AffixPrism controls/windows remain exempt. A visible-window-only mouse-state timer is used because a proposed global hook was rejected by automatic approval review. In-game interaction has not been manually verified. Header context menu adds Reset default filters and Open item wiki.

## Comparison findings and outstanding work

Awakened POE Trade documents transient/persistent windows, wiki access, adjustable shortcuts, widgets and pseudo filters. Its guide explicitly describes selecting synergistic filters rather than automatic certainty about value. Exiled Exchange 2 extends that workflow for POE2 with richer item-specific presets and editors.

PoE Overlay's FAQ describes a model trained on real sales for non-exchange estimates. AffixPrism has no equivalent sales dataset/model: its suggestions use a limited sample of asking prices. These are not feature-equivalent valuations.

Remaining substantial gaps: full pseudo-stat coverage, special unique/crafting presets, quality-normalised and edited-item searches, live bulk-exchange offers, complete gem variant pricing, map-danger checks, configurable utility shortcuts/widgets, broader market history, translations, supported authentication/verification flow, and comprehensive live-market validation.

Suggested next priorities: live diagnostics and price-match validation; complete price-search category support; opt-in map danger highlighting; configurable utility shortcuts; notes attached to bookmarked items. Avoid adding automated in-game actions or background searches merely to match a feature list.

This review does not claim full parity or bug-free operation.

## Parity implementation progress

Implemented in the next pass: individual elemental/chaos resistance totals; Strength/Dexterity/Intelligence/all-attribute totals; life and mana totals including attribute contribution; global flat energy shield total; bookmark notes retained across repeated saves; a crafting-base filter preset retaining exact base and item level. The crafting preset retains rarity/corruption restrictions and is not an unrestricted crafting-market search. Conditional modifiers are intentionally excluded from these aggregate rules. More pseudo rules remain.

Validation: 180 core checks and WPF smoke test passed. Live verification of provider pseudo-stat IDs remains outstanding. Full parity is not complete; sales-trained valuation additionally requires a legitimate sales dataset or service that we do not currently have.
