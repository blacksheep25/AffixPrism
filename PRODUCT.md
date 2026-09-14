> Historical design/review notes. Some implementation status and instructions below are superseded by the root README and docs/FEATURE_AUDIT.md.

# Product

<!-- impeccable:product-schema 1 -->

## Platform
Windows desktop.

## Stack
Implementation choice: C#/.NET 8 and WPF for native Windows overlay controls. No third-party runtime packages.

## Users
The user plays Path of Exile 2 on Windows and wants item price checking and contextual reward guidance.

## Product Purpose
Inspect copied items and show valuable Expedition rewards when entering a Logbook area. Future extensions include Delirium and Ritual.

## Operating Context
Game installation: the user-selected Path of Exile 2 installation. Read Client.txt, without modifying game files. User confirmed Logbook areas specifically.

## Capabilities and Constraints
Item check is the first/default tab. User-requested configurable one-key-combination flow defaults to Alt+E and sends one Ctrl+C action while POE2 is focused, captures fresh item text and opens a browser trade query. The Expedition watchlist has a separate automatic guide window. Live in-overlay pricing and modifier matching remain unresolved. English clipboard format initially. Historical logs identify ExpeditionLogBook_Atoll and ExpeditionLogBook_Tropical; current-version validation remains open. Signed-in trade execution and actual current-game input are not automated-test claims.

League selection is a non-editable dropdown populated from the public economy league feed, with caching and an explicitly labeled offline fallback. Item capture uses frame-spaced input, fresh clipboard sequencing and game-focus validation. Local diagnostics contain timing/stage codes only.

## Brand Commitments
User selected a compact dark panel and confirmed AffixPrism as the application/project name.

## Product Principles
- Keep game visibility and input available.
- Never invent market prices.
- Make manual values and stale information explicit.
- Distinguish Logbook areas from encounters within normal maps.

## Current implementation scope

The expanded prototype adds supported currency/unique market estimates, editable basic trade filters, recent checks, pinned text comparison, Expedition/Delirium/Ritual market guides and in-memory session/explicit loot tracking. Logged Expedition entry loads a separate reward price guide with manual shortlist fallback. Full POE Overlay II parity remains a longer-term objective. README.md is the authoritative current behavior and limitation inventory.
