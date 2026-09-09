> Historical design/review notes. Some implementation status and instructions below are superseded by the root README and docs/FEATURE_AUDIT.md.

# Overlay design

User-pinned direction: compact dark panel that blends into the game.
Operate mode: a narrow native utility panel, with flat rows, right-aligned prices and a restrained warm accent for actions. No decorative imagery.

Surface #15191E; input #222830; foreground #EDF0F2; secondary #B3BDC7; accent #E7BD78; divider #39424C. Segoe UI, 13px body, 22px section title. 20px outer spacing and 8px control gaps. Native keyboard focus, scrolling and resizing remain available.

Item check is the first/default tab. Hover an item and press the configurable Alt+E shortcut to copy it and open a browser trade search. The Expedition tab edits the watchlist; a separate compact guide appears automatically in Logbook areas without changing the selected main tab. Settings includes a keyboard shortcut recorder. Ctrl+Alt+O toggles the main panel; Ctrl+Alt+L toggles its click-through. The headers drag their panels and the tray provides recovery and exit. Live prices inside the overlay remain unavailable and are labeled as such.

League is a dark native ComboBox with keyboard selection, an explicit empty prompt, a refresh button and source/offline status. Item-copy failures identify their stage so the user can distinguish input delivery, clipboard access and item parsing.

## Expanded prototype, September 2026

Current workflow supersedes earlier browser-only descriptions: Alt+E requests in-panel market estimates for supported items. Main panel is 720 by 760 with a 600 minimum width; the separate guide stays 350 by 350. Tabs are Item check, Shortlist, Compare, Guides, Session, Settings. Trade listing navigation is explicit. Estimates disclose source, currency and freshness. Rare valuation, modifier filters and automatic in-map Ritual/Delirium detection remain pending. See README.md for the current feature and validation inventory.

## Evaluation surface

User's supplied POE Overlay II screenshot now pins the Alt+E composition: dedicated vertical evaluator, near-black background, warm item-name border, centered properties and violet modifier text, aligned right-side trade controls, estimate band and lower results area. Main dashboard remains a separate management surface. Default evaluator is 590x850, constrained to work-area height when shown; minimum is 480x520. Scroll the item card independently so estimate and trade action remain reachable. Unsupported filters and listings must be disclosed, never simulated.

## Price-first visual revision

User explicitly rejected the earlier UI and requested closer imitation of POE Overlay. Both windows now use near-black/olive surfaces and muted gold controls. Evaluation uses dense centered serif item text, violet modifiers, an orange unique header, narrow right-side controls and a price range plus variant table beneath the card. Primary action refreshes in place. The website action is secondary. All displayed price rows must be actual provider estimates except clearly marked smoke fixtures.

## Local evaluator interaction model

Min/max fields now align with numeric item rows. Clicking item text includes/excludes a filter and changes its highlight; focusing a field includes it. Exact and Broad presets update local minima, drafts follow recent items, and the result controls persist their choices. Pricing/data are deferred states. Use near-black panels, muted gold UI controls, serif item text and rarity-specific titles. The settings gear and dark scrollbars avoid native bright chrome.

## Compact evaluator reference, September 9

The latest supplied POE Overlay II image pins a compact item-first layout: centered item card, aligned right-hand min/max fields, fixed estimate and Search below it. The evaluator now uses a 620x740 initial size, screen bounds on show/drag, independent item scrolling and an optional bounded listings panel. Filter fields have 28px height, select-all on focus, and Enter searches. Multi-value lines expose remaining numeric bounds through an Edit values expander. Main item text remains the include/exclude toggle. No changes to trade matching or valuation semantics.
