$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = $PSScriptRoot
$output = Join-Path $root 'dist'
$bin = Join-Path $root 'bin'
$staging = Join-Path $output 'SHINO_CODEX_MONITOR_V5_0_WINDOWS'
$archive = Join-Path $output 'SHINO_CODEX_MONITOR_V5_0_WINDOWS.zip'
$report = Join-Path $bin 'V4_TEST_REPORT.txt'
$exe = Join-Path $bin 'SHINO-CODEX-MONITOR.exe'
try {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (!(Test-Path -LiteralPath $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
    if (!(Test-Path -LiteralPath $csc)) { throw 'Compilateur C# .NET Framework Windows indisponible.' }
    New-Item -ItemType Directory -Path $bin -Force | Out-Null
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report -Force }
    $files = @('AssemblyInfo.cs','Models.cs','WakeGuard.cs','RuntimeMetrics.cs','CodexReader.cs','LocalStore.cs','HistoryWindow.cs','AlertRules.cs','QuotaTrayIcon.cs','CircularGauge.cs','DesktopGeometry.cs','SourceHealth.cs','MonitorForm.cs','ReleaseUpdater.cs','ReleasePackage.cs','UpdateHandoff.cs','UpdateDownloadSmoke.cs','SelfTests.cs','Program.cs')
    $sources = @($files | ForEach-Object { Join-Path $root ('src\' + $_) })
    foreach ($source in $sources) { if (!(Test-Path -LiteralPath $source)) { throw ('Source manquante : ' + $source) } }
    $argsCSC = @('/nologo','/codepage:65001','/target:winexe','/platform:x64','/optimize+',
        ('/win32icon:' + (Join-Path $root 'SHINO-CODEX.ico')),
        ('/win32manifest:' + (Join-Path $root 'app.manifest')),
        ('/out:' + $exe),
        '/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll',
        '/reference:System.Windows.Forms.dll','/reference:System.Web.Extensions.dll',
        '/reference:Microsoft.CSharp.dll','/reference:System.IO.Compression.dll',
        '/reference:System.IO.Compression.FileSystem.dll') + $sources
    Write-Host 'Compilation Windows...' -ForegroundColor Cyan
    & $csc @argsCSC
    if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $exe)) { throw 'Compilation impossible.' }
    $proc = Start-Process -FilePath $exe -ArgumentList '--self-test-silent' -Wait -PassThru
    if ($proc.ExitCode -ne 0 -or !(Test-Path -LiteralPath $report)) { throw 'Autotests C# non executes ou en echec.' }
    $result = [IO.File]::ReadAllText($report)
    Write-Host $result
    if (!$result.Contains('98/98') -or $result.Contains('FAIL ')) { throw 'Autotests Windows incomplets.' }
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    foreach ($file in @('INSTALLER-V5.0.bat','INSTALLER-V5.0.ps1','DESINSTALLER-V5.0.ps1','SHINO-CODEX.ico','README-FR.txt')) {
        Copy-Item -LiteralPath (Join-Path $root $file) -Destination $staging
    }
    Copy-Item -LiteralPath $exe -Destination $staging
    $hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToUpperInvariant()
    [IO.File]::WriteAllText((Join-Path $staging 'SHA256SUMS.txt'), ($hash + '  SHINO-CODEX-MONITOR.exe' + "`n"), (New-Object System.Text.UTF8Encoding($false)))
    # Validate installer without touching the currently installed release.
    & (Join-Path $staging 'INSTALLER-V5.0.ps1') -ValidateOnly
    if ($LASTEXITCODE -ne 0) { throw 'Validation de paquet echouee.' }
    if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
    Compress-Archive -LiteralPath $staging -DestinationPath $archive -CompressionLevel Optimal
    Write-Host ('RELEASE READY: ' + $archive) -ForegroundColor Green
    exit 0
}
catch { Write-Host ('ECHEC : ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }
