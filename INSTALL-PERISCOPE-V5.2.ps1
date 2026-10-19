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
$runName = 'CODEX_PERISCOPE'
$legacyRunName = 'SHINO_CODEX_MONITOR_V4'
$stage = Join-Path $env:TEMP ('SHINO-CODEX-VERIFY-' + [guid]::NewGuid().ToString('N'))
$exeChanged = $false
$hadOld = $false
try {
    Write-Host 'CODEX PERISCOPE 5.2.0 - paquet precompile' -ForegroundColor Cyan
    foreach ($relative in @($exeName,'CODEX-PERISCOPE.ico','UNINSTALL-PERISCOPE-V5.2.ps1','README-EN.txt','SHA256SUMS.txt')) {
        if (!(Test-Path -LiteralPath (Join-Path $package $relative))) { throw ('Missing package file: ' + $relative) }
    }
    $line = @(Get-Content -LiteralPath $manifest | Where-Object { $_ -match ('\s+' + [regex]::Escape($exeName) + '$') })
    if ($line.Count -ne 1) { throw 'Missing or duplicate SHA-256 entry.' }
    $hashMatch = [regex]::Match($line[0], '^([A-Fa-f0-9]{64})\s+')
    if (!$hashMatch.Success) { throw 'Invalid SHA-256 checksum.' }
    $expected = $hashMatch.Groups[1].Value.ToUpperInvariant()
    # Validate the package version before touching any installed application.
    $asm = [Reflection.AssemblyName]::GetAssemblyName($sourceExe)
    if ($asm.Version.ToString(3) -ne '5.2.0') {
        throw 'Wrong executable: version 5.2.0 required (installation canceled).'
    }
    $observed = (Get-FileHash -LiteralPath $sourceExe -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($expected -ne $observed) { throw 'SHA-256 mismatch: package is damaged or changed.' }
    Write-Host 'SHA-256 verified (integrity only, not a publisher signature).' -ForegroundColor Green
    if (@(Get-Process -Name 'SHINO-CODEX-MONITOR' -ErrorAction SilentlyContinue).Count -gt 0) {
        throw 'The widget is running. Exit completely from the tray menu, then retry installation.'
    }

    # Always validate a copy BEFORE touching installed binaries; self-test writes a local report.
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    $testExe = Join-Path $stage $exeName
    Copy-Item -LiteralPath $sourceExe -Destination $testExe
    $process = Start-Process -FilePath $testExe -ArgumentList '--self-test-silent' -PassThru -Wait
    $testReport = Join-Path $stage 'V4_TEST_REPORT.txt'
    if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $testReport)) { throw 'Windows self-tests failed (nothing installed).'}
    $result = [IO.File]::ReadAllText($testReport)
    if (!$result.Contains('98/98') -or $result.Contains('FAIL ')) { throw 'Incomplete Windows self-tests. Installation canceled.' }
    Write-Host 'Windows self-tests: 98/98 PASS.' -ForegroundColor Green
    $taskReport = Join-Path $stage 'V5_TASK_TEST_REPORT.txt'
    $taskProcess = Start-Process -FilePath $testExe -ArgumentList '--live-task-self-test' -PassThru -Wait
    if ($taskProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $taskReport)) {
        throw 'Task self-tests failed (nothing installed).'
    }
    $taskResult = [IO.File]::ReadAllText($taskReport)
    if (!$taskResult.Contains('46/46') -or $taskResult.Contains('FAIL ')) {
        throw 'Incomplete task self-tests. Installation canceled.'
    }
    Write-Host 'Task self-tests: 46/46 PASS.' -ForegroundColor Green
    if ($ValidateOnly) { Write-Host 'Validation only: nothing installed.' -ForegroundColor Green; exit 0 }

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
        Copy-Item -LiteralPath (Join-Path $package 'CODEX-PERISCOPE.ico') -Destination $destination -Force
        Copy-Item -LiteralPath (Join-Path $package 'UNINSTALL-PERISCOPE-V5.2.ps1') -Destination $destination -Force
        Copy-Item -LiteralPath (Join-Path $package 'README-EN.txt') -Destination $destination -Force
    }
    finally { Remove-Item -LiteralPath $pendingExe -Force -ErrorAction SilentlyContinue }

    # Verify the installed EXE byte-for-byte; never override Windows warnings.
    $installedHash = (Get-FileHash -LiteralPath $installedExe -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($installedHash -ne $expected) {
        throw 'Installed executable failed verification: rollback required.'
    }
    Write-Host 'The unsigned application may trigger a standard Windows security prompt.' -ForegroundColor Yellow

    $shell = New-Object -ComObject WScript.Shell
    # Remove only the known legacy Start Menu shortcuts after replacement.
    foreach ($oldLink in @('SHINO Codex Monitor.lnk','Desinstaller SHINO Codex Monitor.lnk')) {
        $legacyShortcut = Join-Path $startMenu $oldLink
        if (Test-Path -LiteralPath $legacyShortcut) {
            Remove-Item -LiteralPath $legacyShortcut -Force
        }
    }
    $shortcut = $shell.CreateShortcut((Join-Path $startMenu 'Codex Periscope.lnk'))
    $shortcut.TargetPath = $installedExe
    $shortcut.WorkingDirectory = $destination
    $shortcut.IconLocation = $installedExe + ',0'
    $shortcut.Description = 'Independent Windows quota and activity radar for Codex'
    $shortcut.Save()
    $shortcut = $shell.CreateShortcut((Join-Path $startMenu 'Uninstall Codex Periscope.lnk'))
    $shortcut.TargetPath = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $shortcut.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $destination 'UNINSTALL-PERISCOPE-V5.2.ps1') + '"'
    $shortcut.WorkingDirectory = $destination
    $shortcut.Save()

    if (!(Test-Path -LiteralPath $uninstallKey)) { New-Item -Path $uninstallKey -Force | Out-Null }
    New-ItemProperty -Path $uninstallKey -Name DisplayName -Value 'CODEX PERISCOPE' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name DisplayVersion -Value '5.2.0' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name Publisher -Value 'Codex Periscope contributors' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name DisplayIcon -Value ($installedExe + ',0') -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name InstallLocation -Value $destination -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
    $uninstall = '"' + (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') + '" -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $destination 'UNINSTALL-PERISCOPE-V5.2.ps1') + '"'
    New-ItemProperty -Path $uninstallKey -Name UninstallString -Value $uninstall -PropertyType String -Force | Out-Null

    # Preserve existing startup entries. Migration requires explicit user
    # consent from the in-app Windows Startup menu. Do not create autostart.
    $existingStart = (Get-ItemProperty -Path $runKey -Name $runName -ErrorAction SilentlyContinue).$runName
    $legacyStart = (Get-ItemProperty -Path $runKey -Name $legacyRunName -ErrorAction SilentlyContinue).$legacyRunName
    if ($existingStart -or $legacyStart) {
        Write-Host 'Existing Windows startup entries preserved. Use the app menu to migrate with consent.' -ForegroundColor Yellow
    }
    Write-Host ('Installation successful: ' + $installedExe) -ForegroundColor Green
    Write-Host 'Settings and history preserved. No startup entry created.' -ForegroundColor Cyan
    Start-Process -FilePath $installedExe
    exit 0
}
catch {
    if ($exeChanged -and $hadOld -and (Test-Path -LiteralPath $backupExe)) {
        try { Copy-Item -LiteralPath $backupExe -Destination $installedExe -Force; Write-Host 'Previous application restored.' -ForegroundColor Yellow }
        catch { Write-Host 'Automatic restoration failed; consult README-EN.txt.' -ForegroundColor Red }
    }
    Write-Host ('ERROR: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
finally { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue }
