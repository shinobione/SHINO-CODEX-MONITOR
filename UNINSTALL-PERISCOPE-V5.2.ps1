param([switch]$Detached)
$ErrorActionPreference = 'Stop'
$install = Join-Path $env:LOCALAPPDATA 'Programs\SHINO Codex Monitor'
$target = Join-Path $install 'SHINO-CODEX-MONITOR.exe'
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runNames = @('CODEX_PERISCOPE','SHINO_CODEX_MONITOR_V4','SHINO_CODEX_MONITOR')

# Execute a temporary copy so the original directory can be removed cleanly.
if (-not $Detached) {
    $copy = Join-Path $env:TEMP ('SHINO-CODEX-UNINSTALL-' + [guid]::NewGuid().ToString('N') + '.ps1')
    Copy-Item -LiteralPath $PSCommandPath -Destination $copy
    try {
        Set-Location -LiteralPath $env:TEMP
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $copy -Detached
    } finally { Remove-Item -LiteralPath $copy -Force -ErrorAction SilentlyContinue }
    exit $LASTEXITCODE
}
try {
    Write-Host 'CODEX PERISCOPE — Uninstall' -ForegroundColor Cyan
    Write-Host "Install folder: $install"
    Write-Host 'Settings and local quota history will be KEPT.'
    $reply = Read-Host 'Confirm app removal (type YES)'
    if ($reply -cne 'YES') { Write-Host 'Canceled.'; exit 0 }
    if (@(Get-Process -Name 'SHINO-CODEX-MONITOR' -ErrorAction SilentlyContinue).Count -gt 0) {
        throw 'Exit the widget from the system tray first.'
    }
    Set-Location -LiteralPath $env:TEMP
    if (Test-Path -LiteralPath $install) { Remove-Item -LiteralPath $install -Recurse -Force }
    # Remove only startup entries pointing to this exact executable.
    foreach ($keyName in $runNames) {
        $existing = (Get-ItemProperty -Path $runKey -Name $keyName -ErrorAction SilentlyContinue).$keyName
        if ($existing -and $existing.Trim().Trim('"').Equals($target, [StringComparison]::OrdinalIgnoreCase)) {
            Remove-ItemProperty -Path $runKey -Name $keyName -ErrorAction Stop
        }
    }
    foreach ($name in @('Codex Periscope.lnk','Uninstall Codex Periscope.lnk')) {
        $lnk = Join-Path $startMenu $name
        if (Test-Path -LiteralPath $lnk) { Remove-Item -LiteralPath $lnk -Force }
    }
    $uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SHINO Codex Monitor'
    if (Test-Path -LiteralPath $uninstallKey) { Remove-Item -LiteralPath $uninstallKey -Recurse -Force }
    Write-Host 'Application uninstalled. settings.json and history.jsonl were preserved.' -ForegroundColor Green
    Read-Host 'Press Enter to close' | Out-Null
    exit 0
}
catch {
    Write-Host ('ERROR: ' + $_.Exception.Message) -ForegroundColor Red
    Read-Host 'Press Enter to close' | Out-Null
    exit 1
}
