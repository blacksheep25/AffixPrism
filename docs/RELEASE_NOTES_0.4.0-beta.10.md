# AffixPrism v0.4.0-beta.10

Packaging fix for the AffixPrism rebrand.

- Removed the obsolete compatibility launcher and old-brand runtime references.
- Release builds now start from a clean build folder and reject unexpected launchers, preventing stale files from entering future downloads.
- Removed development symbols containing local source paths from the download.
- Removed the old-profile compatibility layer; existing AffixPrism settings and bookmarks remain untouched.

**Update:** in AffixPrism, open **Settings → Application updates**, enable beta releases, check for updates, then download and choose **Install update and restart**. You can also extract the complete ZIP manually and run **AffixPrism.exe**.

Windows x64; .NET is bundled. The included `createdump.exe` is the .NET crash helper, not another application launcher.
