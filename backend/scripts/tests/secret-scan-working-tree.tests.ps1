<#
.SYNOPSIS
    Dependency-free self-test proving TASK-0032's fix: gate 9 (Secret scan) now sees the working
    tree, not only committed history.

.DESCRIPTION
    Same hand-rolled harness style as gate-summary.tests.ps1 / postgres-test-connection.tests.ps1 —
    assertions plus Write-Host, no Pester (this machine has only the Windows-bundled Pester 3.4.0
    and no `pwsh`).

    THE BUG THIS PROVES WAS REAL: `ci.ps1`'s Secret scan gate used to run only
    `gitleaks detect --source . --config .gitleaks.toml ...`, which scans committed history. A
    secret introduced by an uncommitted change is invisible to it — that is why TASK-0027 closed
    with gate 9 green and TASK-0028 dispatch 1 found it red having touched none of the flagged
    files: the gate always fired one card late, against the previous card's author, never the one
    that introduced the problem.

    This suite builds a THROWAWAY git repository (never the real one — `git init` in a temp
    directory, one committed baseline file), plants a realistic-looking secret in a SECOND file
    that is deliberately never staged or committed, and runs gitleaks against it two ways:

      (a) git-mode `detect` (no `--no-git`) — proves the PRE-FIX behaviour still holds for that
          invocation alone: it does not see the uncommitted file, exit 0.
      (b) `detect --no-git` — proves the FIX: it does see the same uncommitted file, non-zero exit.

    A third case proves the fixture is not vacuously red: a clean uncommitted file (nothing
    secret-shaped in it) must still pass `--no-git`.

    Uses the REAL committed `backend/.gitleaks.toml` as the config for all three cases, so this
    proves the actual rule set the real gate runs, not a toy config invented for the test.

.EXAMPLE
    ./scripts/tests/secret-scan-working-tree.tests.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$testsRoot = $PSScriptRoot
$scriptsRoot = Split-Path -Parent $testsRoot
$backendRoot = Split-Path -Parent $scriptsRoot
$configPath = Join-Path $backendRoot '.gitleaks.toml'

if (-not (Test-Path -LiteralPath $configPath)) {
    Write-Host "FAIL: cannot find .gitleaks.toml at '$configPath'." -ForegroundColor Red
    exit 1
}

$script:failures = [System.Collections.Generic.List[string]]::new()
$script:assertionCount = 0

function Assert-True {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )
    $script:assertionCount++
    if (-not $Condition) {
        $script:failures.Add($Message)
        Write-Host "FAIL: $Message" -ForegroundColor Red
    }
    else {
        Write-Host "ok:   $Message" -ForegroundColor DarkGray
    }
}

$gitleaks = Get-Command gitleaks -ErrorAction SilentlyContinue
if (-not $gitleaks) {
    # Mirrors ci.ps1's own graceful degrade: a WARNING, not a silent pass counted as green. This
    # suite cannot prove anything about a tool that is not present to invoke.
    Write-Host 'WARNING: gitleaks is not installed, so this self-test DID NOT RUN.' -ForegroundColor Yellow
    exit 0
}

function New-FixtureRepo {
    # A throwaway git repository standing in for the real one — one committed baseline file, so
    # git-mode `detect` has real history to scan (not just "zero commits", which would report exit
    # 0 for an uninteresting reason and prove nothing about visibility into an uncommitted file).
    $repo = Join-Path ([System.IO.Path]::GetTempPath()) ("gras-secretscan-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $repo -Force | Out-Null

    Push-Location $repo
    try {
        git init --quiet | Out-Null
        # Repo-local, not global: this must not depend on (or mutate) whatever git identity is
        # configured on the machine running it.
        git config user.email 'selftest@example.invalid' | Out-Null
        git config user.name 'selftest' | Out-Null
        Set-Content -LiteralPath (Join-Path $repo 'README.md') -Value '# fixture repo, TASK-0032 self-test' -NoNewline
        git add README.md | Out-Null
        git commit --quiet -m 'baseline' | Out-Null
    }
    finally {
        Pop-Location
    }

    return $repo
}

function Invoke-GitleaksExitCode {
    param(
        [Parameter(Mandatory)][string]$RepoPath,
        [Parameter(Mandatory)][string]$Config,
        [switch]$NoGit
    )

    Push-Location $RepoPath
    try {
        if ($NoGit) {
            gitleaks detect --source . --no-git --config $Config --no-banner --redact | Out-Null
        }
        else {
            gitleaks detect --source . --config $Config --no-banner --redact | Out-Null
        }
        return $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}

# A realistic-looking fake credential matching this project's own `bearer-or-api-key-literal`
# rule (backend/.gitleaks.toml) — not a placeholder shape, so it is not accidentally suppressed by
# any allowlist entry meant for documented placeholders. Generated at RUN TIME (never a literal in
# this file's own source): a static 16+ char alnum literal sitting here would itself be a matching
# secret-shaped string in a tracked file, which is exactly the family of finding this whole card is
# about clearing deliberately rather than accidentally reintroducing.
$fakeSecretValue = [guid]::NewGuid().ToString('N')
$fakeSecret = "apiKey: `"$fakeSecretValue`""

# ── (a)/(b): the same uncommitted file, scanned both ways ──────────────────────────────────────
$repo = New-FixtureRepo
try {
    Set-Content -LiteralPath (Join-Path $repo 'leaked.txt') -Value $fakeSecret -NoNewline
    # Deliberately neither `git add`-ed nor committed: this is the exact "dispatch under test"
    # shape the card describes — a change nobody has committed yet.

    $gitModeExit = Invoke-GitleaksExitCode -RepoPath $repo -Config $configPath
    Assert-True ($gitModeExit -eq 0) `
        "PRE-FIX BEHAVIOUR, still true of THIS invocation alone: git-mode 'gitleaks detect' (no --no-git) does not see an uncommitted file's secret (exit $gitModeExit)."

    $noGitExit = Invoke-GitleaksExitCode -RepoPath $repo -Config $configPath -NoGit
    Assert-True ($noGitExit -ne 0) `
        "THE FIX: 'gitleaks detect --no-git' DOES see the same uncommitted file's secret (exit $noGitExit)."
}
finally {
    Remove-Item -Recurse -Force $repo -ErrorAction SilentlyContinue
}

# ── (c) control: a clean uncommitted file must not fail --no-git ───────────────────────────────
# Without this, (b) above could pass for the wrong reason -- a --no-git invocation that fails on
# ANY working tree, secret or not, would satisfy (b) vacuously.
$cleanRepo = New-FixtureRepo
try {
    Set-Content -LiteralPath (Join-Path $cleanRepo 'clean.txt') -Value 'nothing secret-shaped lives in this file.' -NoNewline

    $cleanExit = Invoke-GitleaksExitCode -RepoPath $cleanRepo -Config $configPath -NoGit
    Assert-True ($cleanExit -eq 0) `
        "control: '--no-git' over a clean uncommitted file exits 0 (got $cleanExit) -- (b) above is not vacuously red."
}
finally {
    Remove-Item -Recurse -Force $cleanRepo -ErrorAction SilentlyContinue
}

Write-Host ''
if ($script:failures.Count -gt 0) {
    Write-Host "FAILED: $($script:failures.Count) of $script:assertionCount assertion(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host "PASSED: $script:assertionCount assertion(s)." -ForegroundColor Green
exit 0
