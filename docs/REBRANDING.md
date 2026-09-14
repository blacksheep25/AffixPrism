# AffixPrism rename transition

Version 0.4.0-beta.9 introduces AffixPrism and the new GitHub update feed.

- First launch copies previous LocalAppData settings/history when no AffixPrism profile exists. It skips updater jobs and linked files, never overwrites an existing profile, and leaves originals intact.
- Both application names share a legacy instance mutex to avoid duplicate shortcuts.
- The transition release includes identical AffixPrism and ExileLens ZIPs and a legacy-named launcher. The old alias allows installation before the repository rename.
- After GitHub is renamed, users still on ExileLens must manually install AffixPrism once: the old updater strictly validates the previous repository URL. Redirects cannot satisfy that check.
- AffixPrism checks the new repository directly and accepts both repository names for transition assets. Later packages can omit the legacy alias.

The Git history is retained. Current branding and screenshots use AffixPrism; compatibility names are intentional.
