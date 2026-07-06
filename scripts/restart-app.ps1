$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$processes = Get-Process TaskbarLyrics.App -ErrorAction SilentlyContinue
foreach ($process in $processes) {
    if ($process.MainWindowHandle -ne 0) {
        $null = $process.CloseMainWindow()
    }
}

if ($processes) {
    Start-Sleep -Milliseconds 800
    $processes | Where-Object { -not $_.HasExited } | Stop-Process -Force
}

dotnet run --project TaskbarLyrics.App
