# Application updates

Settings → Application updates checks this project's GitHub releases at startup by default. Manual checks are available. Automatic downloads are optional and off by default; beta releases are included by default while ExileLens is in beta. Installing always requires **Update and restart**.

The updater chooses a newer semantic version with matching Windows x64 ZIP and checksum assets. It downloads to LocalAppData, verifies SHA-256, rejects unsafe archive paths, and stages beside the installation. The helper starts before the app exits, swaps the staged directory into place, and keeps the previous installation. If launch fails or the new process exits within five seconds, it restores the old directory. This detects startup failures, not every possible runtime fault.

Settings and bookmarks remain in LocalAppData. Installation needs write access to the parent installation directory. Restricted PowerShell policies may block the helper; the app checks the helper handshake and remains open if it cannot start. Installation logs are stored under `%LOCALAPPDATA%\ExileLens\updates`. Backups are retained rather than silently deleted.

Existing beta.6 installations need one manual upgrade to the first release containing this updater. The updater is included starting with beta.7. ZIP checksums protect against corruption; binaries remain unsigned.

Validation includes version/channel selection, checksum failure, archive traversal rejection, valid extraction, and disposable Windows installation/rollback fixtures. Real GitHub upgrade testing requires a later updater-enabled release.

## Trade progress

One item search can fetch up to three pages of ten listings. When local filters reject results on an earlier page, a later page may be required. Each page respects endpoint spacing and server cooldowns. Progress now identifies the page and counts down the actual remaining delay; it does not silently retry a failed request. A cached result returns before catalogue loading, avoiding unnecessary network waits.
