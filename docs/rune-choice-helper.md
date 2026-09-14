# Rune choice helper

Open **Guides → Rune choice helper · live prices**, or use the AffixPrism tray menu. The tray action also pauses existing scanning.

1. Set your league in AffixPrism Settings.
2. Open the Runeshape reward menu in POE2.
3. Select choice region, then drag around the reward names and quantity prefixes. Exclude the rune-symbol recipe columns where possible.
4. Start live prices and return to the game. The helper hides itself; labels appear outside the selected region and do not intercept clicks.

The selected physical screen rectangle persists. Reselect after moving the game, changing display scaling, resolution or menu position. Capture only runs when the POE2 process is foreground and the rectangle is within its window. Opening the helper again pauses scanning. Scanning does not automatically resume after application restart.

## Prices and recognition

- Local English Tesseract OCR, approximately one scan per 1.5 seconds; no overlapping OCR jobs.
- Images remain in memory. Only the last recognised text is shown in the helper's Recognition details; no production screenshot/debug dumps.
- Exact names first; conservative fuzzy matching is labelled. Tier and ambiguity checks reject uncertain identities. Missing recognition never produces a guessed zero price.
- Explicit `2x` quantities display per-item and total value.
- Prices come from the existing poe.ninja exchange client, keyed by league and category. A 15-minute cache cycle uses the shared request cooldown and stale-cache fallback. Scanning never calls trade search.
- Runes, Currency, UncutGems, Expedition, Ritual, Breach, Verisium, Idols, SoulCores, Essences, LineageSupportGems, Abyss and Fragments cover the menu's possible rewards. Unavailable categories are reported.
- No executable exchange orders or guaranteed sale values. No rune-symbol decoding or automatic item selection.

Leave approximately 300 physical pixels to the right or left of the selected region for labels. If neither side fits, the latest results are available in the helper window. English recognition depends on legible names and sufficient UI scale.

## Reference and validation

Workflow reviewed against [RuneHelper](https://github.com/Denzeriko/RuneHelper), MIT, commit `02713872fc07826f25153bea3019849ec64fb7ec`. AffixPrism uses an independent WPF implementation and the Tesseract .NET wrapper. Reference code is not bundled; the smoke test generates its own OCR image.

Core tests cover exact/fuzzy/tier/ambiguous matching, low OCR confidence and quantity totals. The WPF smoke test runs the actual bundled native OCR engine against RuneHelper's public sample menu: all ten names were recognised in verification. Rendered labels were checked for overlap. This does not substitute for calibrating the user's live game region or verifying current market availability.
