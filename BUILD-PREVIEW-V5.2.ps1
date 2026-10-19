$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = $PSScriptRoot
$output = Join-Path $root 'dist'
$bin = Join-Path $root 'bin'
$staging = Join-Path $output 'CODEX_PERISCOPE_V5_2_PREVIEW_WINDOWS'
$archive = Join-Path $output 'CODEX_PERISCOPE_V5_2_PREVIEW_WINDOWS.zip'
$report = Join-Path $bin 'V4_TEST_REPORT.txt'
$exe = Join-Path $bin 'CODEX-PERISCOPE.exe'
try {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (!(Test-Path -LiteralPath $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
    if (!(Test-Path -LiteralPath $csc)) { throw 'Windows C# .NET Framework compiler not found.' }
    New-Item -ItemType Directory -Path $bin -Force | Out-Null
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report -Force }
    $files = @('AssemblyInfo.cs','Models.cs','WakeGuard.cs','RuntimeMetrics.cs','CodexReader.cs','LiveTaskMonitor.cs','ActivityGlyph.cs','LiveTaskTests.cs','LocalStore.cs','HistoryWindow.cs','AlertRules.cs','QuotaTrayIcon.cs','CircularGauge.cs','DesktopGeometry.cs','SourceHealth.cs','MonitorForm.cs','ReleaseUpdater.cs','ReleasePackage.cs','UpdateHandoff.cs','UpdateDownloadSmoke.cs','SelfTests.cs','Program.cs')
    $sources = @($files | ForEach-Object { Join-Path $root ('src\' + $_) })
    foreach ($source in $sources) { if (!(Test-Path -LiteralPath $source)) { throw ('Missing source: ' + $source) } }
    $identity = [IO.File]::ReadAllText((Join-Path $root 'src\AssemblyInfo.cs'))
    if (!$identity.Contains('AssemblyVersion("5.2.0.0")') -or
        !$identity.Contains('AssemblyInformationalVersion("5.2.0-preview.1")')) {
        throw 'Refusing build due to inconsistent preview version.'
    }
    $argsCSC = @('/nologo','/codepage:65001','/target:winexe','/platform:x64','/optimize+',
        ('/win32icon:' + (Join-Path $root 'CODEX-PERISCOPE.ico')),
        ('/win32manifest:' + (Join-Path $root 'app.manifest')),
        ('/out:' + $exe),
        '/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll',
        '/reference:System.Windows.Forms.dll','/reference:System.Web.Extensions.dll',
        '/reference:Microsoft.CSharp.dll','/reference:System.IO.Compression.dll',
        '/reference:System.IO.Compression.FileSystem.dll') + $sources
    Write-Host 'Building Codex Periscope V5.2.0 Preview...' -ForegroundColor Cyan
    & $csc @argsCSC
    if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $exe)) { throw 'C# compilation failed.' }
    $proc = Start-Process -FilePath $exe -ArgumentList '--self-test-silent' -Wait -PassThru
    if ($proc.ExitCode -ne 0 -or !(Test-Path -LiteralPath $report)) { throw 'Core C# self-tests failed.' }
    $result = [IO.File]::ReadAllText($report)
    Write-Host $result
    if (!$result.Contains('98/98') -or $result.Contains('FAIL ')) { throw 'Core Windows tests incomplete.' }
    $taskReport = Join-Path $bin 'V5_TASK_TEST_REPORT.txt'
    if (Test-Path -LiteralPath $taskReport) { Remove-Item -LiteralPath $taskReport -Force }
    $taskProc = Start-Process -FilePath $exe -ArgumentList '--live-task-self-test' -Wait -PassThru
    if ($taskProc.ExitCode -ne 0 -or !(Test-Path -LiteralPath $taskReport)) { throw 'Task self-tests failed.' }
    $taskResult = [IO.File]::ReadAllText($taskReport)
    if (!$taskResult.Contains('46/46') -or $taskResult.Contains('FAIL ')) { throw 'Task self-tests incomplete.' }
    # Seven allowlisted, English-only Preview files; no legacy scripts.
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    foreach ($file in @('INSTALLER-V5.2.bat','INSTALLER-V5.2.ps1',
        'DESINSTALLER-V5.2.ps1','CODEX-PERISCOPE.ico','README.txt')) {
        Copy-Item -LiteralPath (Join-Path $root $file) -Destination $staging
    }
    Copy-Item -LiteralPath $exe -Destination $staging
    $hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToUpperInvariant()
    [IO.File]::WriteAllText((Join-Path $staging 'SHA256SUMS.txt'),
        ($hash + '  CODEX-PERISCOPE.exe' + [Environment]::NewLine),
        (New-Object System.Text.UTF8Encoding($false)))
    $observed = (Get-FileHash -LiteralPath (Join-Path $staging 'CODEX-PERISCOPE.exe') -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($observed -ne $hash) { throw 'Executable checksum mismatch.' }
    & (Join-Path $staging 'INSTALLER-V5.2.ps1') -ValidateOnly
    if ($LASTEXITCODE -ne 0) { throw 'Installer dry-run validation failed.' }
    if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
    Compress-Archive -LiteralPath $staging -DestinationPath $archive -CompressionLevel Optimal
    if (!(Test-Path -LiteralPath $archive)) { throw 'Preview ZIP missing.' }
    Write-Host ('V5.2 PREVIEW READY (NOT PUBLISHED): ' + $archive) -ForegroundColor Green
    exit 0
}
catch { Write-Host ('ERROR: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }
