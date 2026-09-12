# Release process

GitHub release notes must contain only the approved, user-facing changes for that version. Never paste the entire cumulative CHANGELOG.md or automatically generated commit history into a release. Keep previous versions in CHANGELOG.md for reference. Use the approved version-specific notes file as the release body.

1. Update `Directory.Build.props`, `CHANGELOG.md`, and the packaging script's default version.
2. Run the core tests and Windows UI smoke from the repository root; inspect `artifacts/smoke-result.txt` and changed UI captures.
3. Run `./scripts/package-release.ps1 -Version <version>` in PowerShell. This creates a Windows x64 self-contained ZIP and SHA-256 file under `artifacts`.
4. Extract into a fresh directory, run the extracted executable with `--smoke-test` using the repository root as its working directory, then verify normal startup. Keep private saved data out of the package.
5. Obtain the user's explicit approval before pushing or creating a release. After approval, commit/push and require a successful Windows CI build for that commit. Tag that exact commit and create a GitHub release with the changelog, ZIP and checksum. Mark experimental versions as prereleases.

The ZIP includes .NET; native OCR may still require the Visual C++ 2015–2022 x64 runtime. Extract all files. Exit the running tray process before switching versions. Settings stay under LocalAppData, outside the installation directory. Packages are unsigned; no installer or update service is currently included.

## Manual game/hardware checklist

Record OS, screen arrangement, DPI and taskbar position for each run. Automated geometry checks do not replace these tests.

- Capture with Alt+E in borderless POE2, then click back into the game; evaluation should hide and the game should accept input.
- Move evaluation/comparison across monitors with different scaling. Close/reopen, unplug a monitor, and use Reset window placement. Controls should remain reachable.
- Right-click the tray icon on each taskbar edge and a secondary monitor; every menu entry should remain above the taskbar.
- Inspect known and ambiguous socket tooltips after a listing fetch; copied inference must stay labelled.
- Force a profile folder write failure in a disposable Windows account, then restore access and retry. Do not damage a real profile file.
- Confirm actual live search/error/cooldown behaviour without repeatedly hammering the trade service.

Real-game/hardware validation is not claimed by the automated beta checks.
