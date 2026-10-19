param([switch]$ValidateOnly)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$package = $PSScriptRoot
$destination = Join-Path $env:LOCALAPPDATA 'Programs\SHINO Codex Monitor'
# Legacy location intentionally retained during the V5.1 -> V5.2 migration.
# Data and app path compatibility are more important than renaming a hidden folder.
$exeName = 'CODEX-PERISCOPE.exe'
$sourceExe = Join-Path $package $exeName
$installedExe = Join-Path $destination $exeName
$legacyExe = Join-Path $destination 'SHINO-CODEX-MONITOR.exe'
$backupExe = Join-Path $destination 'CODEX-PERISCOPE.previous.exe'
$manifest = Join-Path $package 'SHA256SUMS.txt'
$stage = Join-Path $env:TEMP ('CODEX-PERISCOPE-VERIFY-' + [guid]::NewGuid().ToString('N'))
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$newUninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Codex Periscope'
$oldUninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SHINO Codex Monitor'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runName = 'CODEX_PERISCOPE'
$oldRunName = 'SHINO_CODEX_MONITOR_V4'
$oldRunNameV3 = 'SHINO_CODEX_MONITOR'
$changed = $false
$hadNew = $false

try {
    Write-Host 'CODEX PERISCOPE V5.2.0 PREVIEW — verified installation' -ForegroundColor Cyan
    foreach ($file in @($exeName,'CODEX-PERISCOPE.ico','UNINSTALL-V5.2.ps1','README.txt','SHA256SUMS.txt')) {
        if (!(Test-Path -LiteralPath (Join-Path $package $file))) { throw ("Missing package file: " + $file) }
    }
    $line = @(Get-Content -LiteralPath $manifest | Where-Object { $_ -match ('\s+' + [regex]::Escape($exeName) + '$') })
    if ($line.Count -ne 1) { throw 'Missing or duplicate SHA-256 manifest entry.' }
    $match = [regex]::Match($line[0], '^([0-9a-fA-F]{64})\s+')
    if (!$match.Success) { throw 'Invalid SHA-256 manifest.' }
    $expected = $match.Groups[1].Value.ToUpperInvariant()
    $version = [Reflection.AssemblyName]::GetAssemblyName($sourceExe).Version.ToString(3)
    if ($version -ne '5.2.0') { throw 'Expected Codex Periscope version 5.2.0; refusing install.' }
    if ((Get-FileHash -LiteralPath $sourceExe -Algorithm SHA256).Hash.ToUpperInvariant() -ne $expected) {
        throw 'SHA-256 mismatch. Package may be damaged or modified.'
    }
    foreach ($processName in @('SHINO-CODEX-MONITOR','CODEX-PERISCOPE')) {
        if (@(Get-Process -Name $processName -ErrorAction SilentlyContinue).Count -gt 0) {
            throw 'A Codex Monitor is running. Exit it from the tray and retry.'
        }
    }
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    $testExe = Join-Path $stage $exeName
    Copy-Item -LiteralPath $sourceExe -Destination $testExe
    $q = Start-Process -FilePath $testExe -ArgumentList '--self-test-silent' -Wait -PassThru
    $qReport = Join-Path $stage 'V4_TEST_REPORT.txt'
    if ($q.ExitCode -ne 0 -or !(Test-Path -LiteralPath $qReport) -or
        !([IO.File]::ReadAllText($qReport)).Contains('98/98')) {
        throw 'Quota self-tests failed. Nothing was installed.'
    }
    $t = Start-Process -FilePath $testExe -ArgumentList '--live-task-self-test' -Wait -PassThru
    $tReport = Join-Path $stage 'V5_TASK_TEST_REPORT.txt'
    if ($t.ExitCode -ne 0 -or !(Test-Path -LiteralPath $tReport) -or
        !([IO.File]::ReadAllText($tReport)).Contains('46/46')) {
        throw 'Task self-tests failed. Nothing was installed.'
    }
    if ($ValidateOnly) {
        Write-Host 'Validation PASS: SHA-256, identity, 98 quota tests and 46 task tests. No installation performed.' -ForegroundColor Green
        exit 0
    }

    $migrateStartup = $false
    $previousRun = (Get-ItemProperty -Path $runKey -Name $oldRunName -ErrorAction SilentlyContinue).$oldRunName
    if (!$previousRun) { $previousRun = (Get-ItemProperty -Path $runKey -Name $oldRunNameV3 -ErrorAction SilentlyContinue).$oldRunNameV3 }
    if ($previousRun -and $previousRun.Trim().Trim('"').Equals($legacyExe, [StringComparison]::OrdinalIgnoreCase)) {
        $reply = Read-Host 'Move your existing Windows autostart entry to Codex Periscope? (Y/N)'
        $migrateStartup = $reply -eq 'Y'
    }
    elseif ($previousRun) {
        Write-Warning 'Unrecognized old startup path found. It will not be touched.'
    }

    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $pending = Join-Path $destination ($exeName + '.pending')
    $hadNew = Test-Path -LiteralPath $installedExe
    Copy-Item -LiteralPath $sourceExe -Destination $pending -Force
    try {
        if ($hadNew) {
            [IO.File]::Replace($pending, $installedExe, $backupExe, $true)
        }
        else {
            [IO.File]::Move($pending, $installedExe)
        }
        $changed = $true
    }
    finally { Remove-Item -LiteralPath $pending -Force -ErrorAction SilentlyContinue }
    if ((Get-FileHash -LiteralPath $installedExe -Algorithm SHA256).Hash.ToUpperInvariant() -ne $expected) {
        throw 'Installed executable failed SHA-256 verification.'
    }
    Copy-Item -LiteralPath (Join-Path $package 'CODEX-PERISCOPE.ico') -Destination $destination -Force
    Copy-Item -LiteralPath (Join-Path $package 'UNINSTALL-V5.2.ps1') -Destination $destination -Force
    Copy-Item -LiteralPath (Join-Path $package 'README.txt') -Destination $destination -Force

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut((Join-Path $startMenu 'Codex Periscope.lnk'))
    $shortcut.TargetPath = $installedExe
    $shortcut.WorkingDirectory = $destination
    $shortcut.IconLocation = $installedExe + ',0'
    $shortcut.Description = 'Independent Codex quota and local activity radar'
    $shortcut.Save()
    $shortcut = $shell.CreateShortcut((Join-Path $startMenu 'Uninstall Codex Periscope.lnk'))
    $shortcut.TargetPath = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $shortcut.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $destination 'UNINSTALL-V5.2.ps1') + '"'
    $shortcut.Save()

    if (!(Test-Path -LiteralPath $newUninstallKey)) { New-Item -Path $newUninstallKey -Force | Out-Null }
    New-ItemProperty -Path $newUninstallKey -Name DisplayName -Value 'Codex Periscope' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $newUninstallKey -Name DisplayVersion -Value '5.2.0' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $newUninstallKey -Name Publisher -Value 'Independent project' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $newUninstallKey -Name DisplayIcon -Value ($installedExe + ',0') -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $newUninstallKey -Name InstallLocation -Value $destination -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $newUninstallKey -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $newUninstallKey -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
    $uninstall = '"' + (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') +
        '" -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $destination 'UNINSTALL-V5.2.ps1') + '"'
    New-ItemProperty -Path $newUninstallKey -Name UninstallString -Value $uninstall -PropertyType String -Force | Out-Null

    # Retire only the known predecessor's registration/shortcuts after success.
    if (Test-Path -LiteralPath $oldUninstallKey) { Remove-Item -LiteralPath $oldUninstallKey -Recurse -Force }
    foreach ($oldShortcut in @('SHINO Codex Monitor.lnk','Desinstaller SHINO Codex Monitor.lnk')) {
        $oldPath = Join-Path $startMenu $oldShortcut
        if (Test-Path -LiteralPath $oldPath) {
            $shortcut = $shell.CreateShortcut($oldPath)
            $target = $shortcut.TargetPath
            if ($target -and ($target.StartsWith($destination, [StringComparison]::OrdinalIgnoreCase) -or
                $oldShortcut -like 'Desinstaller*')) {
                Remove-Item -LiteralPath $oldPath -Force
            }
        }
    }
    if ($migrateStartup) {
        if (!(Test-Path -LiteralPath $runKey)) { New-Item -Path $runKey -Force | Out-Null }
        New-ItemProperty -Path $runKey -Name $runName -Value ('"' + $installedExe + '"') -PropertyType String -Force | Out-Null
        foreach ($name in @($oldRunName,$oldRunNameV3)) {
            $entry = (Get-ItemProperty -Path $runKey -Name $name -ErrorAction SilentlyContinue).$name
            if ($entry -and $entry.Trim().Trim('"').Equals($legacyExe, [StringComparison]::OrdinalIgnoreCase)) {
                Remove-ItemProperty -Path $runKey -Name $name -Force
            }
        }
    }
    Write-Host ('Codex Periscope installed: ' + $installedExe) -ForegroundColor Green
    Write-Host 'Existing user settings and quota history were preserved.' -ForegroundColor Cyan
    if ($previousRun -and !$migrateStartup) { Write-Warning 'Old autostart entry was left unchanged at your request.' }
    Start-Process -FilePath $installedExe
    exit 0
}
catch {
    if ($changed) {
        try {
            if ($hadNew -and (Test-Path -LiteralPath $backupExe)) {
                Copy-Item -LiteralPath $backupExe -Destination $installedExe -Force
            }
            elseif (!$hadNew -and (Test-Path -LiteralPath $installedExe)) {
                Remove-Item -LiteralPath $installedExe -Force
            }
            Write-Host 'Executable rollback attempted.' -ForegroundColor Yellow
        } catch { Write-Warning 'Automatic executable rollback failed. Use the previous backup.' }
    }
    Write-Host ('ERROR: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
finally { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue }
