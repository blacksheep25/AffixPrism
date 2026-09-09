> Historical design/review notes. Some implementation status and instructions below are superseded by the root README and docs/FEATURE_AUDIT.md.

# Item metadata display

Settings > Item metadata colours lists supported types, colours and use cases. The shared ItemMetadata registry drives evaluator cards, previews and numerical comparisons.

The adapter preserves GGG ItemMod flags: fractured, mutated, crafted, desecrated and vestigial. Unknown true flags retain their names in tooltips. Double corruption uses the documented doubleCorrupted field, with the previous alias accepted.

Source: https://www.pathofexile.com/developer/docs/reference#type-ItemMod

The palette covers explicit/implicit, enchant, rune, crafted, desecrated, fractured, mutated, vestigial, bonded, scourge, crucible, utility, cosmetic, properties/classes, flavour/descriptions, corruption/double corruption, unidentified, sanctified, mirrored, split, unmodifiable and veiled states. Legacy and unusual colours are overlay choices, not verified pixel-exact game colours. Unknown metadata remains neutral and visible. Colours indicate source or state, not desirability.

Clipboard text without tags cannot reliably identify modifier origin. Tier and roll metadata remain in tooltips. This audit covers line metadata; it does not claim exhaustive rendering of every nested inventory, socketed-item, gem or per-value property structure in the API.

Validation: 133 checks and the WPF smoke suite passed.
