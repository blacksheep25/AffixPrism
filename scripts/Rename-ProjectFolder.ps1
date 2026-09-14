# Run after closing the app and tools holding the project folder open.
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\')
$parent=Split-Path -Parent $source
$target=Join-Path $parent 'AffixPrism'
if((Split-Path -Leaf $source) -eq 'AffixPrism') { Write-Output 'Project already renamed.'; exit }
if((Split-Path -Leaf $source) -ne 'ExileLens' -or !(Test-Path -LiteralPath (Join-Path $source '.git')) -or (Test-Path -LiteralPath $target)) { throw 'Unexpected source or destination. No changes made.' }
if((Split-Path -Parent ([IO.Path]::GetFullPath($target))) -ne $parent) { throw 'Destination leaves the project parent.' }
Set-Location -LiteralPath $parent
Rename-Item -LiteralPath $source -NewName 'AffixPrism'
Write-Output "Project renamed to $target"
