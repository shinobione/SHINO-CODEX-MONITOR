$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = $PSScriptRoot
$output = Join-Path $root 'dist'
$bin = Join-Path $root 'bin'
$staging = Join-Path $output 'CODEX_PERISCOPE_V5_2_PREVIEW_WINDOWS'
$archive = Join-Path $output 'CODEX_PERISCOPE_V5_2_PREVIEW_WINDOWS.zip'
$report = Join-Path $bin 'V4_TEST_REPORT.txt'
$exe = Join-Path $bin 'SHINO-CODEX-MONITOR.exe'
try {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (!(Test-Path -LiteralPath $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
    if (!(Test-Path -LiteralPath $csc)) { throw 'Windows .NET Framework C# compiler unavailable.' }
    New-Item -ItemType Directory -Path $bin -Force | Out-Null
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report -Force }
    $files = @('AssemblyInfo.cs','Models.cs','WakeGuard.cs','RuntimeMetrics.cs','CodexReader.cs','LiveTaskMonitor.cs','ActivityGlyph.cs','LiveTaskTests.cs','LocalStore.cs','HistoryData.cs','HistoryWindow.cs','PremiumTests.cs','P0Tests.cs','InstrumentTests.cs','InstrumentStyle.cs','TargetSource.cs','TargetTests.cs','MissionPresentation.cs','PeriscopeWindow.cs','AlertRules.cs','QuotaTrayIcon.cs','CircularGauge.cs','DesktopGeometry.cs','SourceHealth.cs','MonitorForm.cs','ReleaseUpdater.cs','ReleasePackage.cs','UpdateHandoff.cs','UpdateDownloadSmoke.cs','SelfTests.cs','Program.cs')
    $sources = @($files | ForEach-Object { Join-Path $root ('src\' + $_) })
    foreach ($source in $sources) { if (!(Test-Path -LiteralPath $source)) { throw ('Missing source: ' + $source) } }
    $identity = [IO.File]::ReadAllText((Join-Path $root 'src\AssemblyInfo.cs'))
    if (!$identity.Contains('AssemblyVersion("5.2.0.0")') -or
        !$identity.Contains('AssemblyInformationalVersion("5.2.0-preview.1")')) {
        throw 'Refusing build: the version does not match V5.2 Preview.'
    }
    & (Join-Path $root 'GENERATE-PERISCOPE-ICON.ps1')
    if (!(Test-Path -LiteralPath (Join-Path $root 'CODEX-PERISCOPE.ico'))) {
        throw 'Radar icon generation failed.'
    }
    # Validate the real rasterized frames (16/32/48/256), not just ICO metadata.
    & (Join-Path $root 'TEST-PERISCOPE-RADAR.ps1')
    $argsCSC = @('/nologo','/codepage:65001','/target:winexe','/platform:x64','/optimize+',
        ('/win32icon:' + (Join-Path $root 'CODEX-PERISCOPE.ico')),
        ('/win32manifest:' + (Join-Path $root 'app.manifest')),
        ('/out:' + $exe),
        '/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll',
        '/reference:System.Windows.Forms.dll','/reference:System.Web.Extensions.dll',
        '/reference:Microsoft.CSharp.dll','/reference:System.IO.Compression.dll',
        '/reference:System.IO.Compression.FileSystem.dll') + $sources
    Write-Host 'Building Codex Periscope V5.2 Preview...' -ForegroundColor Cyan
    & $csc @argsCSC
    if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $exe)) { throw 'Compilation failed.' }
    $proc = Start-Process -FilePath $exe -ArgumentList '--self-test-silent' -Wait -PassThru
    if ($proc.ExitCode -ne 0 -or !(Test-Path -LiteralPath $report)) { throw 'C# self-tests did not run or failed.' }
    $result = [IO.File]::ReadAllText($report)
    Write-Host $result
    if (!$result.Contains('98/98') -or $result.Contains('FAIL ')) { throw 'Windows self-tests incomplete.' }
    $taskReport = Join-Path $bin 'V5_TASK_TEST_REPORT.txt'
    if (Test-Path -LiteralPath $taskReport) { Remove-Item -LiteralPath $taskReport -Force }
    $taskProc = Start-Process -FilePath $exe -ArgumentList '--live-task-self-test' -Wait -PassThru
    if ($taskProc.ExitCode -ne 0 -or !(Test-Path -LiteralPath $taskReport)) { throw 'Task self-tests did not run.' }
    $taskResult = [IO.File]::ReadAllText($taskReport)
    if (!$taskResult.Contains('71/71') -or $taskResult.Contains('FAIL ')) { throw 'Task self-tests incomplete.' }
    $premiumReport = Join-Path $bin 'PREMIUM_TEST_REPORT.txt'
    if (Test-Path -LiteralPath $premiumReport) { Remove-Item -LiteralPath $premiumReport -Force }
    $premiumProc = Start-Process -FilePath $exe -ArgumentList '--premium-self-test' -WindowStyle Hidden -Wait -PassThru
    if ($premiumProc.ExitCode -ne 0 -or !(Test-Path -LiteralPath $premiumReport)) { throw 'Premium history/radar tests failed.' }
    $premiumResult = [IO.File]::ReadAllText($premiumReport)
    Write-Host $premiumResult
    if (!$premiumResult.Contains('All premium checks passed') -or $premiumResult.Contains('FAIL ')) { throw 'Premium tests incomplete.' }
    $p0Report = Join-Path $bin 'P0_TEST_REPORT.txt'
    if (Test-Path -LiteralPath $p0Report) { Remove-Item -LiteralPath $p0Report -Force }
    $p0Proc = Start-Process -FilePath $exe -ArgumentList '--p0-self-test' -WindowStyle Hidden -Wait -PassThru
    if ($p0Proc.ExitCode -ne 0 -or !(Test-Path -LiteralPath $p0Report)) { throw 'P0 multi-task/window icon tests failed.' }
    $p0Result = [IO.File]::ReadAllText($p0Report)
    Write-Host $p0Result
    if (!$p0Result.Contains('All P0 checks passed') -or $p0Result.Contains('FAIL ')) { throw 'P0 tests incomplete.' }
    $instrumentReport = Join-Path $bin 'INSTRUMENT_TEST_REPORT.txt'
    if (Test-Path -LiteralPath $instrumentReport) { Remove-Item -LiteralPath $instrumentReport -Force }
    $instrumentProc = Start-Process -FilePath $exe -ArgumentList '--instrument-self-test' -WindowStyle Hidden -Wait -PassThru
    if ($instrumentProc.ExitCode -ne 0 -or !(Test-Path -LiteralPath $instrumentReport)) {
        if (Test-Path -LiteralPath $instrumentReport) { Get-Content -LiteralPath $instrumentReport }
        throw 'Full-window instrument tests failed.'
    }
    $instrumentResult = [IO.File]::ReadAllText($instrumentReport)
    Write-Host $instrumentResult
    if (!$instrumentResult.Contains('All instrument checks passed') -or $instrumentResult.Contains('FAIL ')) { throw 'Instrument checks incomplete.' }
    $targetReport = Join-Path $bin 'TARGET_TEST_REPORT.txt'
    if (Test-Path -LiteralPath $targetReport) { Remove-Item -LiteralPath $targetReport -Force }
    $targetProc = Start-Process -FilePath $exe -ArgumentList '--target-self-test' -WindowStyle Hidden -Wait -PassThru
    if ($targetProc.ExitCode -ne 0 -or !(Test-Path -LiteralPath $targetReport)) { throw 'Final target tests failed.' }
    $targetResult = [IO.File]::ReadAllText($targetReport)
    Write-Host $targetResult
    if ($targetResult.Contains('FAIL ') -or !$targetResult.Contains('final target checks passed')) { throw 'Final target tests incomplete.' }
    $captureProc = Start-Process -FilePath $exe -ArgumentList '--target-captures' -WindowStyle Hidden -Wait -PassThru
    if ($captureProc.ExitCode -ne 0) { throw 'Final target captures failed.' }
    # V5.2 preview installer; never mix with V5.1 files.
    $resolvedOutput = [IO.Path]::GetFullPath($output).TrimEnd('\') + '\'
    $resolvedStaging = [IO.Path]::GetFullPath($staging)
    if (!$resolvedStaging.StartsWith($resolvedOutput, [StringComparison]::OrdinalIgnoreCase)) { throw 'Staging path is outside dist.' }
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    foreach ($file in @('CODEX-PERISCOPE.ico','README-EN.txt',
        'INSTALL-PERISCOPE-V5.2.bat','INSTALL-PERISCOPE-V5.2.ps1',
        'UNINSTALL-PERISCOPE-V5.2.ps1')) {
        Copy-Item -LiteralPath (Join-Path $root $file) -Destination $staging
    }
    Copy-Item -LiteralPath $exe -Destination $staging
    $hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToUpperInvariant()
    [IO.File]::WriteAllText((Join-Path $staging 'SHA256SUMS.txt'),
        ($hash + '  SHINO-CODEX-MONITOR.exe' + [Environment]::NewLine),
        (New-Object System.Text.UTF8Encoding($false)))
    # Exact dry run: never modify an existing V5.1 installation during CI.
    & (Join-Path $staging 'INSTALL-PERISCOPE-V5.2.ps1') -ValidateOnly
    if ($LASTEXITCODE -ne 0) { throw 'V5.2 installer validation failed.' }
    if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $archive
    if (!(Test-Path -LiteralPath $archive)) { throw 'Preview archive missing.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        $allowed = @('SHINO-CODEX-MONITOR.exe','CODEX-PERISCOPE.ico','README-EN.txt',
            'INSTALL-PERISCOPE-V5.2.bat','INSTALL-PERISCOPE-V5.2.ps1',
            'UNINSTALL-PERISCOPE-V5.2.ps1','SHA256SUMS.txt')
        if ($zip.Entries.Count -ne $allowed.Count) { throw 'Unexpected Preview ZIP entry count.' }
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName -cnotin $allowed) { throw ('Unexpected Preview ZIP entry: ' + $entry.FullName) }
        }
        $stream = $zip.GetEntry('SHINO-CODEX-MONITOR.exe').Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $zipHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
        finally { $stream.Dispose(); $sha.Dispose() }
        if ($zipHash -ne $hash) { throw 'Executable bytes in Preview ZIP differ from validated bytes.' }
        Write-Host 'Preview ZIP verified: seven allowlisted files; executable SHA-256 matches validated image.'
    }
    finally { $zip.Dispose() }
    Write-Host ('PREVIEW BUILT: ' + $archive) -ForegroundColor Green
    exit 0
}
catch { Write-Host ('ERROR: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }

