param([Parameter(Mandatory=$true)][string]$Manifest)
$ErrorActionPreference='Stop'
$job=Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
$target=[IO.Path]::GetFullPath($job.Target).TrimEnd('\')
$stage=[IO.Path]::GetFullPath($job.Stage).TrimEnd('\')
$backup=$target+'.previous-'+[Guid]::NewGuid().ToString('N')
$log=Join-Path (Split-Path -Parent $Manifest) 'install-result.txt'
try {
    if($target -eq [IO.Path]::GetPathRoot($target) -or -not(Test-Path -LiteralPath (Join-Path $target 'ExileLens.exe')) -or -not(Test-Path -LiteralPath (Join-Path $stage 'ExileLens.exe'))) { throw 'Invalid update paths' }
    if((Split-Path -Parent $stage) -ne (Split-Path -Parent $target) -or -not((Split-Path -Leaf $stage).StartsWith('.ExileLens-update-'))) { throw 'Unexpected staging location' }
    if((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked installation folders are not supported' }
    'ready' | Set-Content -LiteralPath ($Manifest+'.ready')
    Wait-Process -Id $job.ProcessId -Timeout 60 -ErrorAction SilentlyContinue
    if(Get-Process -Id $job.ProcessId -ErrorAction SilentlyContinue) { throw 'ExileLens did not exit' }
    Move-Item -LiteralPath $target -Destination $backup
    try {
        Move-Item -LiteralPath $stage -Destination $target
        $process=Start-Process -FilePath (Join-Path $target 'ExileLens.exe') -WorkingDirectory $target -WindowStyle Hidden -PassThru
        Start-Sleep -Seconds 5
        if($process.HasExited) { throw 'Updated application exited during startup' }
    } catch {
        if(Test-Path -LiteralPath $target) { Move-Item -LiteralPath $target -Destination ($stage+'-failed') }
        Move-Item -LiteralPath $backup -Destination $target
        throw
    }
    "Update installed. Previous version: $backup" | Set-Content -LiteralPath $log
} catch {
    "Update failed: $($_.Exception.Message)" | Set-Content -LiteralPath $log
    if(-not(Get-Process -Id $job.ProcessId -ErrorAction SilentlyContinue) -and (Test-Path -LiteralPath (Join-Path $target 'ExileLens.exe'))) { Start-Process -FilePath (Join-Path $target 'ExileLens.exe') -WorkingDirectory $target -WindowStyle Hidden }
}
