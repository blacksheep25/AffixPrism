# Price-check QOL

## Filter profiles

In the evaluation window, select the stats you care about, choose **Item values** or **Broad (-10%)**, then open **Profiles**. Name the profile and choose **Save current filters**.

Profiles store stat signatures and modifier kinds for the current item class. Applying one selects matching stats and regenerates minimums from the current item's rolls; it does not carry fixed numeric bounds from the old item. Stats absent from the current item are skipped. Currency/status/age and exact-base selection are not part of the template.

The active profile is reused for newly checked items of the same class. Existing per-item draft filters take precedence. **Apply selected** explicitly reapplies the template. Applying a profile invalidates displayed prices but does not automatically issue a search.

Profiles persist in `%LOCALAPPDATA%/AffixPrism/filter-profiles.json` (up to 30 profiles). A malformed existing file is preserved and profile writes are disabled for that session.

## Active filters and undo

Expand **Active filters** to see the base/rarity/identification/corruption restrictions and selected numeric filters with their minimum and maximum bounds. Click a numeric filter to remove it.

**Undo filters** restores recent filter changes, including numeric bounds, base selection, currency, seller status and listing age. **Ctrl+Z** does the same while the evaluation window has focus and a text field is not being edited. Text fields retain their normal text-edit undo. Undo is scoped to the current displayed item and keeps up to 50 changes. Search explicitly to refresh prices afterwards.

## Map modifier warnings

In **Profiles**, enter warning phrases, one per line. **Save warnings only** updates the selected profile without replacing its filter template.

When you price-check a waystone/map item, matching modifier lines appear in an orange warning panel for the active profile. Matching is literal, case-insensitive substring matching, not a numeric threshold or build-safety assessment. Warnings require copying/price-checking the map item; no map-log modifier detection or automatic loot tracking is involved. No warning means none of your phrases matched, not that the map is safe for your build.

## Recent item navigation

Use the **← / →** buttons beside Bookmark, or **Alt+Left / Alt+Right** while the evaluation window has focus. Navigation uses the existing recent-check history, restores the copied item and its local filter draft, and does not count another check or request fresh prices. Press **Search** when you want current offers.
