# SHINO // CODEX MONITOR - safe GitHub publisher (PowerShell 5.1 compatible)
# This script refuses an unexpected repo, unexpected files and account mismatch.
# The user's existing local Git tree from the previous failed run can be resumed.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$owner = 'shinobione'
$repoName = 'SHINO-CODEX-MONITOR'
$remoteRepo = "$owner/$repoName"
$commitMessage = 'Initial public-safe source and Windows CI'

# Explicit reviewed allowlist: no local settings, history, credentials or build outputs.
$allowed = @(
    '.gitignore', '.gitattributes', 'README.md', 'README-FR.txt', 'SECURITY.md',
    'SHINO-CODEX.ico', 'app.manifest',
    'BUILD-RELEASE-V4.4.bat', 'BUILD-RELEASE-V4.4.ps1',
    'INSTALLER-V4.4.bat', 'INSTALLER-V4.4.ps1',
    'DESINSTALLER-V4.4.ps1', 'PREPARER-ET-INSTALLER-V4.4.bat',
    'PUBLIER-GITHUB.bat', 'PUBLIER-GITHUB.ps1',
    '.github/workflows/build-windows.yml',
    'tests/test_v44_static.py', 'tests/test_public_safety.py', 'tests/core-sha256.json',
    'src/AlertRules.cs', 'src/AssemblyInfo.cs', 'src/CodexReader.cs',
    'src/HistoryWindow.cs', 'src/LocalStore.cs', 'src/Models.cs',
    'src/MonitorForm.cs', 'src/Program.cs', 'src/QuotaTrayIcon.cs',
    'src/RuntimeMetrics.cs', 'src/SelfTests.cs', 'src/WakeGuard.cs'
)

function Test-SameFileSet([string[]]$actual, [string[]]$expected) {
    $differences = @(Compare-Object -ReferenceObject @($expected | Sort-Object) -DifferenceObject @($actual | Sort-Object))
    return ($differences.Count -eq 0)
}

function Git-Result([string[]]$arguments) {
    $savedPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $lines = @(& git @arguments 2>$null)
        $code = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $savedPreference }
    return [pscustomobject]@{ Code = $code; Text = ($lines -join "`n").Trim(); Lines = $lines }
}

try {
    Set-Location -LiteralPath $root
    foreach ($program in @('git', 'gh')) {
        if (-not (Get-Command $program -ErrorAction SilentlyContinue)) {
            throw "$program introuvable. Installer Git et GitHub CLI, puis relancer."
        }
    }

    # Read one user profile, once, and use it for login AND optional numeric ID.
    # Avoid a second 'gh api user' after Git init. Do not print the profile JSON.
    $savedPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $ghOutput = @(& gh api user 2>$null)
        $ghCode = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $savedPreference }
    if ($ghCode -ne 0 -or $ghOutput.Count -eq 0) {
        throw 'GitHub CLI non connecte ou API indisponible. Executer: gh auth status puis gh auth login.'
    }
    try { $identity = ($ghOutput -join "`n") | ConvertFrom-Json }
    catch { throw 'Reponse GitHub illisible. Executer: gh api user.' }
    if ([string]$identity.login -cne $owner) {
        throw ('Compte GitHub incorrect: ' + [string]$identity.login + ' (attendu: ' + $owner + ').')
    }
    $uid = [string]$identity.id
    if ($uid -match '^[0-9]+$') {
        $noreply = ('{0}+{1}@users.noreply.github.com' -f $uid, $owner)
    } else {
        # A numeric ID is optional for creating a Git commit.
        $noreply = ('{0}@users.noreply.github.com' -f $owner)
    }
    Write-Host ('Compte GitHub verifie: ' + $owner) -ForegroundColor Green

    # Refuse public repo overwrite: a 404 should be handled by gh repo create.
    $savedPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & gh repo view $remoteRepo --json nameWithOwner --jq '.nameWithOwner' 2>$null | Out-Null
        $existingRemoteCode = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $savedPreference }
    if ($existingRemoteCode -eq 0) {
        throw "Le depot $remoteRepo existe deja. Publication automatique bloquee."
    }

    # All non-ignored source files must appear in this exact allowlist.
    Write-Host 'Controle strict des fichiers publics...' -ForegroundColor Cyan
    foreach ($name in $allowed) {
        $path = Join-Path $root ($name.Replace('/', '\'))
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw ('Fichier manquant: ' + $name)
        }
    }
    $allFiles = @(Get-ChildItem -LiteralPath $root -Recurse -File -Force)
    foreach ($file in $allFiles) {
        $relative = $file.FullName.Substring($root.Length + 1).Replace('\', '/')
        if ($relative -like '.git/*' -or $relative -like '*/__pycache__/*' -or
            $relative -like '*.pyc' -or $relative -like '*.zip') { continue }
        if ($allowed -notcontains $relative) {
            throw ('Fichier inattendu, non publiable: ' + $relative)
        }
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw ('Lien symbolique interdit: ' + $relative)
        }
        if ($file.Extension.ToLowerInvariant() -eq '.ico') { continue }
        $contents = [IO.File]::ReadAllText($file.FullName)
        $patterns = @(
            'gh[pousr]_[A-Za-z0-9_]{25,}',
            'github_pat_[A-Za-z0-9_]{20,}',
            'sk-(proj-)?[A-Za-z0-9_-]{16,}',
            '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----',
            '(?i)Authorization\s*:\s*Bearer\s+[A-Za-z0-9._-]{12,}'
        )
        foreach ($pattern in $patterns) {
            if ([regex]::IsMatch($contents, $pattern)) {
                throw ('Secret potentiel detecte dans: ' + $relative)
            }
        }
    }
    # Validate SHA-256 of the frozen, audited core sources.
    $hashManifest = (Get-Content -LiteralPath (Join-Path $root 'tests/core-sha256.json') -Raw) | ConvertFrom-Json
    foreach ($entry in $hashManifest.PSObject.Properties) {
        $sourcePath = Join-Path $root ('src/' + $entry.Name)
        $hash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($hash -cne ([string]$entry.Value).ToLowerInvariant()) {
            throw ('Code coeur modifie depuis audit: src/' + $entry.Name)
        }
    }

    $localGit = Join-Path $root '.git'
    if (Test-Path -LiteralPath $localGit) {
        $topResult = Git-Result @('rev-parse', '--show-toplevel')
        if ($topResult.Code -ne 0) { throw 'Depot Git local invalide.' }
        $actualTop = $topResult.Text.Replace('\', '/').TrimEnd('/')
        $expectedTop = ([IO.Path]::GetFullPath($root)).Replace('\', '/').TrimEnd('/')
        if (-not [string]::Equals($actualTop, $expectedTop, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Le depot Git local ne correspond pas a ce dossier.'
        }
        $branch = Git-Result @('symbolic-ref', '--quiet', '--short', 'HEAD')
        if ($branch.Code -ne 0 -or $branch.Text -cne 'main') {
            throw 'Branche Git locale inattendue: publication bloquee.'
        }
        $remotes = Git-Result @('remote')
        if ($remotes.Code -ne 0 -or $remotes.Text.Length -gt 0) {
            throw 'Un remote Git existe deja: publication automatique bloquee.'
        }
        Write-Host 'Reprise du depot Git local initialise lors du premier essai.' -ForegroundColor Cyan
    }
    else {
        # Do not create a nested Git repository accidentally.
        $parentGit = Git-Result @('rev-parse', '--show-toplevel')
        if ($parentGit.Code -eq 0) { throw 'Ce dossier est deja dans un autre depot Git.' }
        & git init -b main
        if ($LASTEXITCODE -ne 0) { throw 'git init a echoue.' }
    }

    $head = Git-Result @('rev-parse', '--verify', 'HEAD')
    $resumeCommit = ($head.Code -eq 0)
    if ($resumeCommit) {
        # A failed 'gh repo create' may leave one local commit. Only allow safe resume.
        $msg = Git-Result @('log', '-1', '--format=%s')
        $committed = Git-Result @('ls-tree', '-r', '--name-only', 'HEAD')
        $dirty = Git-Result @('status', '--porcelain', '--untracked-files=no')
        if ($msg.Text -cne $commitMessage -or $committed.Code -ne 0 -or
            -not (Test-SameFileSet @($committed.Lines) @($allowed)) -or
            $dirty.Text.Length -gt 0) {
            throw 'Le depot local a change apres un commit. Refus de publication automatique.'
        }
        Write-Host 'Commit initial deja present et intact: reprise sure.' -ForegroundColor Cyan
    }
    else {
        & git add -- $allowed
        if ($LASTEXITCODE -ne 0) { throw 'git add a echoue.' }
        $staged = Git-Result @('diff', '--cached', '--name-only')
        if ($staged.Code -ne 0 -or -not (Test-SameFileSet @($staged.Lines) @($allowed))) {
            throw 'Liste des fichiers stages differente de la liste auditee. Publication bloquee.'
        }
        & git config user.name $owner
        if ($LASTEXITCODE -ne 0) { throw 'Configuration du nom Git impossible.' }
        & git config user.email $noreply
        if ($LASTEXITCODE -ne 0) { throw 'Configuration de l email Git impossible.' }
        & git commit -m $commitMessage
        if ($LASTEXITCODE -ne 0) { throw 'Commit Git impossible.' }
    }

    Write-Host 'Creation du depot public et publication des sources auditees...' -ForegroundColor Cyan
    & gh repo create $remoteRepo --public --source . --remote origin --push --description 'Native Windows Codex ChatGPT rate-limit monitor'
    if ($LASTEXITCODE -ne 0) {
        throw 'Echec creation / push. Examiner les remotes et le depot public avant toute nouvelle tentative.'
    }
    Write-Host "DEPOT PUBLIE: https://github.com/$remoteRepo" -ForegroundColor Green
    Write-Host 'Attendre la CI Windows PASS avant toute annonce de Release.' -ForegroundColor Yellow
}
catch {
    Write-Host ('ECHEC: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'Aucune publication ne doit etre consideree comme confirmee sans verification du depot GitHub.' -ForegroundColor Yellow
    exit 1
}
