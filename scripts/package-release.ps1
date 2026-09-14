param([string]$Version = '0.4.0-beta.9')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$') { throw 'Invalid version' }
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $projectRoot
try {
    $output = Join-Path $projectRoot 'artifacts/build'
    if (Get-Process AffixPrism -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $output 'AffixPrism.exe') }) { throw 'Exit the running build before packaging.' }
    $expectedOutput = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts/build'))
    if ([IO.Path]::GetFullPath($output) -ne $expectedOutput -or (Test-Path -LiteralPath $output) -and ((Get-Item -LiteralPath $output).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unsafe build directory' }
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
    dotnet publish src/AffixPrism -c Release -r win-x64 --self-contained true -p:Version=$Version -o $output --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    Copy-Item LICENSE,THIRD_PARTY.md,CHANGELOG.md -Destination $output
    @'
AffixPrism for Windows x64

Extract the entire ZIP, then run AffixPrism.exe. Keep tessdata, ThirdParty and native DLL folders alongside it.
.NET is bundled. Rune OCR may require the Microsoft Visual C++ 2015-2022 x64 runtime.
Select your league and Client.txt path in Settings. Hover an item in POE2 and press Alt+E.
Ctrl+Alt+O restores the panel; use the tray icon to exit before upgrading.
Settings remain in %LOCALAPPDATA%\AffixPrism. Extract upgrades to a new folder; do not merge old binaries.

Experimental beta: trade access and estimates are not guaranteed. Prices are asking prices, not completed sales.
https://github.com/blacksheep25/AffixPrism
'@ | Set-Content (Join-Path $output 'START-HERE.txt')
    $launchers = @(Get-ChildItem -LiteralPath $output -File -Filter '*.exe')
    if (-not (Test-Path -LiteralPath (Join-Path $output 'AffixPrism.exe')) -or @($launchers | Where-Object Name -notin @('AffixPrism.exe','createdump.exe')).Count -gt 0) { throw 'Unexpected launcher in package. Only AffixPrism and the bundled .NET crash helper are allowed.' }
    $archive = Join-Path $projectRoot "artifacts/AffixPrism-$Version-win-x64.zip"
    Compress-Archive -Path (Join-Path $output '*') -DestinationPath $archive -CompressionLevel Optimal -Force
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($archive))" | Set-Content "$archive.sha256" -Encoding ascii
    Write-Output $archive
} finally { Pop-Location }
