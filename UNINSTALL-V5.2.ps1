param([switch]$Detached)
$ErrorActionPreference = 'Stop'
$install = Join-Path $env:LOCALAPPDATA 'Programs\SHINO Codex Monitor'
$target = Join-Path $install 'CODEX-PERISCOPE.exe'
$oldTarget = Join-Path $install 'SHINO-CODEX-MONITOR.exe'
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

# Run outside the directory to allow full removal without admin rights.
if (!$Detached) {
    $copy = Join-Path $env:TEMP ('CODEX-PERISCOPE-UNINSTALL-' + [guid]::NewGuid().ToString('N') + '.ps1')
    Copy-Item -LiteralPath $PSCommandPath -Destination $copy
    try {
        Set-Location -LiteralPath $env:TEMP
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $copy -Detached
    }
    finally { Remove-Item -LiteralPath $copy -Force -ErrorAction SilentlyContinue }
    exit $LASTEXITCODE
}
try {
    Write-Host 'CODEX PERISCOPE — Uninstall' -ForegroundColor Cyan
    Write-Host ("Application folder: " + $install)
    Write-Host 'Your local settings.json and history.jsonl will be preserved.'
    $answer = Read-Host 'Type YES to uninstall Codex Periscope'
    if ($answer -cne 'YES') { Write-Host 'Cancelled.'; exit 0 }
    foreach ($name in @('CODEX-PERISCOPE','SHINO-CODEX-MONITOR')) {
        if (@(Get-Process -Name $name -ErrorAction SilentlyContinue).Count -gt 0) {
            throw 'Exit all Codex Monitor instances from the system tray first.'
        }
    }
    Set-Location -LiteralPath $env:TEMP
    if (Test-Path -LiteralPath $install) { Remove-Item -LiteralPath $install -Recurse -Force }
    foreach ($name in @('CODEX_PERISCOPE','SHINO_CODEX_MONITOR_V4','SHINO_CODEX_MONITOR')) {
        $entry = (Get-ItemProperty -Path $runKey -Name $name -ErrorAction SilentlyContinue).$name
        if ($entry) {
            $path = $entry.Trim().Trim('"')
            if ($path.Equals($target, [StringComparison]::OrdinalIgnoreCase) -or
                $path.Equals($oldTarget, [StringComparison]::OrdinalIgnoreCase)) {
                Remove-ItemProperty -Path $runKey -Name $name -ErrorAction Stop
            }
        }
    }
    foreach ($item in @('Codex Periscope.lnk','Uninstall Codex Periscope.lnk',
                        'SHINO Codex Monitor.lnk','Desinstaller SHINO Codex Monitor.lnk')) {
        $shortcut = Join-Path $startMenu $item
        if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut -Force }
    }
    foreach ($key in @('HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Codex Periscope',
                      'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SHINO Codex Monitor')) {
        if (Test-Path -LiteralPath $key) { Remove-Item -LiteralPath $key -Recurse -Force }
    }
    Write-Host 'Codex Periscope has been uninstalled. Your local settings and history remain intact.' -ForegroundColor Green
    Read-Host 'Press Enter to close' | Out-Null
    exit 0
}
catch {
    Write-Host ('ERROR: ' + $_.Exception.Message) -ForegroundColor Red
    Read-Host 'Press Enter to close' | Out-Null
    exit 1
}
