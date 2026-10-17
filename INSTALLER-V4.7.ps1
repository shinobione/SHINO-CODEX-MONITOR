param([switch]$ValidateOnly)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$package = $PSScriptRoot
$destination = Join-Path $env:LOCALAPPDATA 'Programs\SHINO Codex Monitor'
$exeName = 'SHINO-CODEX-MONITOR.exe'
$sourceExe = Join-Path $package $exeName
$manifest = Join-Path $package 'SHA256SUMS.txt'
$installedExe = Join-Path $destination $exeName
$backupExe = Join-Path $destination 'SHINO-CODEX-MONITOR.previous.exe'
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SHINO Codex Monitor'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runName = 'SHINO_CODEX_MONITOR_V4'
$stage = Join-Path $env:TEMP ('SHINO-CODEX-VERIFY-' + [guid]::NewGuid().ToString('N'))
$exeChanged = $false
$hadOld = $false
try {
    Write-Host 'SHINO // CODEX MONITOR 4.7.1 - paquet precompile' -ForegroundColor Cyan
    foreach ($relative in @($exeName,'SHINO-CODEX.ico','DESINSTALLER-V4.7.ps1','README-FR.txt','SHA256SUMS.txt')) {
        if (!(Test-Path -LiteralPath (Join-Path $package $relative))) { throw ('Fichier absent : ' + $relative) }
    }
    $line = @(Get-Content -LiteralPath $manifest | Where-Object { $_ -match ('\s+' + [regex]::Escape($exeName) + '$') })
    if ($line.Count -ne 1) { throw 'Empreinte SHA-256 absente ou dupliquee.' }
    $hashMatch = [regex]::Match($line[0], '^([A-Fa-f0-9]{64})\s+')
    if (!$hashMatch.Success) { throw 'Empreinte SHA-256 invalide.' }
    $expected = $hashMatch.Groups[1].Value.ToUpperInvariant()
    $observed = (Get-FileHash -LiteralPath $sourceExe -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($expected -ne $observed) { throw 'Empreinte SHA-256 incorrecte : paquet endommage ou modifie.' }
    Write-Host 'Controle SHA-256 : OK (integrite, pas signature du developpeur).' -ForegroundColor Green
    if (@(Get-Process -Name 'SHINO-CODEX-MONITOR' -ErrorAction SilentlyContinue).Count -gt 0) {
        throw 'Le widget est ouvert. Clic droit sur son icone > Quitter completement, puis relancer l installation.'
    }

    # Always validate a copy BEFORE touching installed binaries; self-test writes a local report.
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    $testExe = Join-Path $stage $exeName
    Copy-Item -LiteralPath $sourceExe -Destination $testExe
    $process = Start-Process -FilePath $testExe -ArgumentList '--self-test-silent' -PassThru -Wait
    $testReport = Join-Path $stage 'V4_TEST_REPORT.txt'
    if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $testReport)) { throw 'Autotests Windows : echec (aucune installation).'}
    $result = [IO.File]::ReadAllText($testReport)
    if (!$result.Contains('76/76') -or $result.Contains('FAIL ')) { throw 'Autotests Windows : resultat incomplet. Installation annulee.' }
    Write-Host 'Autotests Windows : 76/76 PASS.' -ForegroundColor Green
    if ($ValidateOnly) { Write-Host 'Verification seulement : rien installe.' -ForegroundColor Green; exit 0 }

    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $hadOld = Test-Path -LiteralPath $installedExe
    $pendingExe = Join-Path $destination ($exeName + '.pending')
    Copy-Item -LiteralPath $sourceExe -Destination $pendingExe -Force
    try {
        # Atomic same-volume replacement on Windows; the previous exe goes to the backup.
        if ($hadOld) {
            [System.IO.File]::Replace($pendingExe, $installedExe, $backupExe, $true)
        } else {
            [System.IO.File]::Move($pendingExe, $installedExe)
        }
        $exeChanged = $true
        Copy-Item -LiteralPath (Join-Path $package 'SHINO-CODEX.ico') -Destination $destination -Force
        Copy-Item -LiteralPath (Join-Path $package 'DESINSTALLER-V4.7.ps1') -Destination $destination -Force
        Copy-Item -LiteralPath (Join-Path $package 'README-FR.txt') -Destination $destination -Force
    }
    finally { Remove-Item -LiteralPath $pendingExe -Force -ErrorAction SilentlyContinue }

    # Verify the installed EXE byte-for-byte before any MOTW operation.
    # Only this already verified local binary may be unblocked; never disable
    # SmartScreen, Windows attachment policies or global security settings.
    $installedHash = (Get-FileHash -LiteralPath $installedExe -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($installedHash -ne $expected) {
        throw 'Verification du binaire installe echouee : retour arriere.'
    }
    try {
        Unblock-File -LiteralPath $installedExe -ErrorAction Stop
        Write-Host 'Marquage Internet retire du binaire installe (si present).' -ForegroundColor Green
    }
    catch {
        # The installed application remains unsigned: do not bypass policy or
        # delete an arbitrary ADS. Report the limitation without hiding errors.
        Write-Warning ('Marquage Internet non retire : ' + $_.Exception.Message)
        Write-Warning 'Un avertissement Windows peut encore apparaitre au lancement.'
    }

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut((Join-Path $startMenu 'SHINO Codex Monitor.lnk'))
    $shortcut.TargetPath = $installedExe
    $shortcut.WorkingDirectory = $destination
    $shortcut.IconLocation = $installedExe + ',0'
    $shortcut.Description = 'Widget Windows local pour les quotas Codex ChatGPT'
    $shortcut.Save()
    $shortcut = $shell.CreateShortcut((Join-Path $startMenu 'Desinstaller SHINO Codex Monitor.lnk'))
    $shortcut.TargetPath = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $shortcut.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $destination 'DESINSTALLER-V4.7.ps1') + '"'
    $shortcut.WorkingDirectory = $destination
    $shortcut.Save()

    if (!(Test-Path -LiteralPath $uninstallKey)) { New-Item -Path $uninstallKey -Force | Out-Null }
    New-ItemProperty -Path $uninstallKey -Name DisplayName -Value 'SHINO // CODEX MONITOR' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name DisplayVersion -Value '4.7.1' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name Publisher -Value 'SHINO (local)' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name DisplayIcon -Value ($installedExe + ',0') -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name InstallLocation -Value $destination -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
    $uninstall = '"' + (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') + '" -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $destination 'DESINSTALLER-V4.7.ps1') + '"'
    New-ItemProperty -Path $uninstallKey -Name UninstallString -Value $uninstall -PropertyType String -Force | Out-Null

    $existingStart = (Get-ItemProperty -Path $runKey -Name $runName -ErrorAction SilentlyContinue).$runName
    if ($existingStart -and $existingStart -notmatch [regex]::Escape($installedExe)) {
        Write-Host 'Autostart existant sur un autre chemin : non modifie. Regler depuis le menu du widget.' -ForegroundColor Yellow
    }
    Write-Host ('Installation OK : ' + $installedExe) -ForegroundColor Green
    Write-Host 'Preferences et historique intacts. Aucun autostart cree.' -ForegroundColor Cyan
    Start-Process -FilePath $installedExe
    exit 0
}
catch {
    if ($exeChanged -and $hadOld -and (Test-Path -LiteralPath $backupExe)) {
        try { Copy-Item -LiteralPath $backupExe -Destination $installedExe -Force; Write-Host 'Ancien programme restaure.' -ForegroundColor Yellow }
        catch { Write-Host 'Restauration automatique impossible : consulter le README.' -ForegroundColor Red }
    }
    Write-Host ('ECHEC : ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
finally { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue }
