[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../artifacts'))
$keepBuild = Join-Path $artifactRoot 'build'
if (-not (Test-Path -LiteralPath (Join-Path $keepBuild 'ExileLens.exe'))) {
    throw 'The latest artifacts/build/ExileLens.exe is missing. Nothing was deleted.'
}

# Artifacts contains generated output and temporary research/checkouts only.
# Keep the current build; include old logs, screenshots and scratch scripts.
$targets = @(Get-ChildItem -LiteralPath $artifactRoot -Force | Where-Object { $_.Name -ne 'build' })
foreach ($target in $targets) {
    $resolved = (Resolve-Path -LiteralPath $target.FullName).Path
    if ((Split-Path -Parent $resolved) -ne $artifactRoot -or $resolved -eq $keepBuild) {
        throw "Unexpected cleanup target: $resolved"
    }
    $entries = @($target)
    if ($target.PSIsContainer) {
        $entries += @(Get-ChildItem -LiteralPath $resolved -Recurse -Force)
    }
    if ($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
        throw "A link was found inside $resolved. Nothing was deleted."
    }
}

if ($targets.Count -eq 0) { Write-Host 'Already clean: only the latest build remains.'; return }
Write-Host "Preserving: $keepBuild"
$targets | Select-Object Name, FullName | Format-Table -AutoSize
if (-not $WhatIfPreference) {
    $answer = Read-Host "Permanently delete these $($targets.Count) artifact entries (including old screenshots and logs)? Type DELETE to continue"
    if ($answer -cne 'DELETE') { Write-Host 'Cancelled. Nothing was deleted.'; return }
}
foreach ($target in $targets) {
    if ($PSCmdlet.ShouldProcess($target.FullName, 'Delete generated or temporary artifact')) {
        Remove-Item -LiteralPath $target.FullName -Recurse -Force
    }
}
Write-Host 'Cleanup finished. artifacts/build and project source files were preserved.'
