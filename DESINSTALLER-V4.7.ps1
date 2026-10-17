param([switch]$Detached)
$ErrorActionPreference = 'Stop'
$install = Join-Path $env:LOCALAPPDATA 'Programs\SHINO Codex Monitor'
$target = Join-Path $install 'SHINO-CODEX-MONITOR.exe'
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runName = 'SHINO_CODEX_MONITOR_V4'

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
    Write-Host 'SHINO // CODEX MONITOR — Desinstallation' -ForegroundColor Cyan
    Write-Host "Dossier : $install"
    Write-Host 'Les preferences et l historique local seront CONSERVES.'
    $reply = Read-Host 'Confirmer la suppression du programme (taper OUI)'
    if ($reply -cne 'OUI') { Write-Host 'Annule.'; exit 0 }
    if (@(Get-Process -Name 'SHINO-CODEX-MONITOR' -ErrorAction SilentlyContinue).Count -gt 0) {
        throw 'Quittez d abord le widget depuis son icone pres de l horloge.'
    }
    Set-Location -LiteralPath $env:TEMP
    if (Test-Path -LiteralPath $install) { Remove-Item -LiteralPath $install -Recurse -Force }
    # Retirer seulement l'autostart qui pointait effectivement vers cette installation.
    $existing = (Get-ItemProperty -Path $runKey -Name $runName -ErrorAction SilentlyContinue).$runName
    if ($existing -and $existing.Trim().Trim('"').Equals($target, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-ItemProperty -Path $runKey -Name $runName -ErrorAction Stop
    }
    foreach ($name in @('SHINO Codex Monitor.lnk','Desinstaller SHINO Codex Monitor.lnk')) {
        $lnk = Join-Path $startMenu $name
        if (Test-Path -LiteralPath $lnk) { Remove-Item -LiteralPath $lnk -Force }
    }
    $uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SHINO Codex Monitor'
    if (Test-Path -LiteralPath $uninstallKey) { Remove-Item -LiteralPath $uninstallKey -Recurse -Force }
    Write-Host 'Programme desinstalle. Les fichiers settings.json et history.jsonl sont intacts.' -ForegroundColor Green
    Read-Host 'Appuyez sur Entree pour fermer' | Out-Null
    exit 0
}
catch {
    Write-Host ('Echec : ' + $_.Exception.Message) -ForegroundColor Red
    Read-Host 'Appuyez sur Entree pour fermer' | Out-Null
    exit 1
}
