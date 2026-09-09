# Changelog

## 0.4.0-beta.2 — 2026-09-09

- Fixed Spirit and other identical-text modifiers being rejected when the trade catalogue contains multiple IDs. Searches require at least one matching ID, with the selected min/max bounds and all other filters preserved.
- Added regression checks for the reported Bloodstone Amulet, including rejection of listings below 50 Spirit.
- 235 core checks pass. Live trade connectivity could not be retested on the development host; the catalogue case was verified against the retained reference snapshot.


## 0.4.0-beta.1 — 2026-09-09

First downloadable Windows x64 beta. Includes the existing item evaluator, comparisons, bookmarks, profiles, Logbook guides, rune choice helper and desktop QOL features.

### Fixed

- Profile save failures no longer display success: changes are explicitly session-only, and successful retries clear the failure.
- Invalid/unreadable profile files stay protected from writes, with a visible explanation in the profile editor.
- Trade HTTP 400 clears cached catalogues for the next manual search; server outages, verification pages and non-object JSON now produce clearer errors without automatic retries.
- Critical additional-arrow/skill comparisons retain modifier kind instead of treating a rune stat as an explicit stat.
- Window placement handles floating-point rounding at 125%/150% scaling without throwing a clamp exception.

### Added

- Price diagnostics show qualifying independent sellers, offers excluded for missing exchange rates, and fetched-versus-total sample coverage.
- Socket artwork can reuse a source-confirmed image for an exact rune name during the session. Inferred identities retain their confidence label; ambiguous names and other tiers do not inherit art. No extra search requests.
- Sanitised/transcribed bow and lineage examples, synthetic socket/currency fixtures, server-error/catalogue-refresh tests, and a labelled similarity regression benchmark.
- Placement checks cover 100%, 125%, 150% and 200% scaling with negative-coordinate monitors and taskbar offsets.
- Repeatable release packaging with bundled .NET runtime, dependency notices, startup instructions and SHA-256 checksum.

### Validation and known limits

232 core checks pass, including all eight synthetic similarity benchmark cases. Windows UI smoke and native OCR tests pass. The benchmark tests matching behaviour, not real market valuation accuracy. Live game/multi-monitor hardware checks and a labelled real-market dataset remain follow-up work. External trade access, exact copied socket identity and image availability are not guaranteed.

## Initial public source — 2026-09-09

MIT-licensed source, feature audit, screenshot gallery and Windows CI. No downloadable release was published for this snapshot.
