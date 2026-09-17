<#
.SYNOPSIS
    Dependency-free self-test for backend/scripts/lib/postgres-test-connection.ps1 (TASK-0031,
    extended by TASK-0078 for the local-container-by-default / -UseHostedDb resolution order).

.DESCRIPTION
    Same hand-rolled harness style as gate-summary.tests.ps1 / local-env.tests.ps1 — this machine
    has only the ancient Windows-bundled Pester 3.4.0 and no `pwsh`, so this is assertions plus
    Write-Host, not a Pester suite.

    Dot-sources lib/postgres-test-connection.ps1 and calls its functions IN-PROCESS, against a
    throwaway temp directory standing in for $HOME — never against the real
    $HOME/.gras/pg-test.txt, so this suite cannot read (or leak) a real credential and passes
    identically whether or not this machine happens to have that file.

    TASK-0078 section (search "TASK-0078" below): proves the file is ignored without -UseHostedDb,
    used with it, that an explicit env var always wins in CI, and that outside CI a non-local
    explicit env var is REJECTED (Initialize-PostgresTestConnection throws) unless -UseHostedDb is
    also passed. Does NOT cover the local-container Docker-endpoint pre-flight probe or its
    unreachable-endpoint failure message — that piece is not implemented by this task; see
    TASK-0078's Log for why and the options reported back to the orchestrator.

    Each fixture file is written byte-for-byte with a leading EF BB BF (the same UTF-8 BOM the real
    file on this machine starts with, confirmed by byte inspection per the task card) using
    [IO.File]::WriteAllBytes, not a text-mode Set-Content — a text writer might apply its own BOM
    policy and mask the very case this suite exists to prove.

    The "never echoed" assertions plant a distinctive marker string
    (Fingerprint=selftest-marker-3fae1c) in the fixture connection string and grep every captured
    output stream (stdout, Write-Host's information stream, and the verbose stream under -Verbose)
    for it — a check that could not tell the difference between "the value never appears" and "we
    never looked" is not a check.

    Exits 0 if every assertion passed, 1 otherwise (each failing assertion printed).

.EXAMPLE
    ./scripts/tests/postgres-test-connection.tests.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$testsRoot = $PSScriptRoot
$scriptsRoot = Split-Path -Parent $testsRoot
$libPath = Join-Path $scriptsRoot 'lib/postgres-test-connection.ps1'

if (-not (Test-Path -LiteralPath $libPath)) {
    Write-Host "FAIL: cannot find postgres-test-connection.ps1 at '$libPath'." -ForegroundColor Red
    exit 1
}

. $libPath

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

function New-FixtureHome {
    # A throwaway directory tree standing in for $HOME, holding .gras/pg-test.txt written with the
    # exact byte-level shape (leading EF BB BF) of the real file, so the BOM-stripping code path is
    # exercised for real rather than assumed.
    param(
        # $null (the default) means no .gras directory is created at all. A [string]-typed
        # Mandatory parameter rejects BOTH $null and an empty string automatically, which is why
        # this is neither Mandatory nor typed [string] — [AllowNull()] on an untyped parameter is
        # what lets a call site pass -Content $null on purpose.
        [AllowNull()]
        $Content = $null
    )

    $fixtureHome = Join-Path ([System.IO.Path]::GetTempPath()) ("gras-selftest-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $fixtureHome -Force | Out-Null

    if ($null -ne $Content) {
        $grasDir = Join-Path $fixtureHome '.gras'
        New-Item -ItemType Directory -Path $grasDir -Force | Out-Null
        $file = Join-Path $grasDir 'pg-test.txt'

        $bom = [byte[]]@(0xEF, 0xBB, 0xBF)
        $bytes = $bom + [System.Text.Encoding]::UTF8.GetBytes($Content)
        [System.IO.File]::WriteAllBytes($file, $bytes)
    }

    return $fixtureHome
}

$marker = 'Host=selftest-marker-3fae1c;Port=5432;Database=schoolmanagement_tests;Username=u;Password=selftest-marker-3fae1c'

# ── (a) file present, BOM-prefixed, trailing CRLF and whitespace ───────────────────────────────────
$homeWithFile = New-FixtureHome -Content "  $marker`r`n`r`n  "
try {
    $result = Get-PostgresTestConnectionFromFile -HomeDirectory $homeWithFile
    Assert-True ($result -eq $marker) "file present: returns the trimmed, BOM-stripped value exactly (got '$result')."
    # NOT $result.StartsWith([char]0xFEFF): .NET's default (culture-aware) string comparison
    # treats U+FEFF as an ignorable/zero-weight character, so StartsWith would return $true here
    # even though no BOM character is actually present -- proven while writing this test (it failed
    # against known-clean input). Comparing the char at index 0 directly has no such culture layer.
    Assert-True ($result[0] -ne [char]0xFEFF) 'file present: no leading BOM character survives.'
    Assert-True ($result[0] -ne ' ') 'file present: no leading whitespace survives.'
    Assert-True ($result[-1] -ne ' ') 'file present: no trailing whitespace survives.'
}
finally {
    Remove-Item -Recurse -Force $homeWithFile -ErrorAction SilentlyContinue
}

# ── (b) no .gras directory at all ───────────────────────────────────────────────────────────────
$homeWithoutFile = New-FixtureHome -Content $null
try {
    $resultMissing = Get-PostgresTestConnectionFromFile -HomeDirectory $homeWithoutFile
    Assert-True ($null -eq $resultMissing) "file absent: returns `$null (got '$resultMissing'), does not throw."
}
finally {
    Remove-Item -Recurse -Force $homeWithoutFile -ErrorAction SilentlyContinue
}

# ── (c) file present but whitespace-only after the BOM ──────────────────────────────────────────
$homeWithBlankFile = New-FixtureHome -Content "   `r`n  "
try {
    $resultBlank = Get-PostgresTestConnectionFromFile -HomeDirectory $homeWithBlankFile
    Assert-True ($null -eq $resultBlank) "file whitespace-only after trim: returns `$null (got '$resultBlank')."
}
finally {
    Remove-Item -Recurse -Force $homeWithBlankFile -ErrorAction SilentlyContinue
}

# ── (d) Initialize-PostgresTestConnection: an explicitly-set env var ALWAYS wins over the file ──
# TASK-0078: the fixture value must be a LOCAL host (this assertion is about file-vs-env
# precedence, not about the new non-local rejection covered separately below) -- a bare opaque
# string like the pre-TASK-0078 fixture used ('already-set-value') has no parseable host, which
# Test-IsLocalPostgresHost now correctly treats as non-local and rejects outside CI. Proven to fail
# against the pre-change resolver: that resolver had no -UseHostedDb/-IsCi parameters at all, so
# this call errors out ("a parameter cannot be found") rather than reaching the assertion.
#
# Every fixture connection string below (this test and the ones that follow) spells its password
# as the literal `***`, never a realistic-looking value: this file's assertions only ever check the
# HOST, so a real-shaped password buys nothing and only trips
# postgres-connection-string-with-password (backend/.gitleaks.toml Family A already allowlists this
# exact literal, `regexTarget = "match"`, both scan passes) — same convention Family A's own
# comment documents for `<pw>`/`YOUR_PASSWORD`/`...`.
$homeForPrecedence = New-FixtureHome -Content $marker
$savedEnv = $env:POSTGRES_TEST_CONNECTION
try {
    $env:POSTGRES_TEST_CONNECTION = 'Host=localhost;Port=5432;Database=already-set;Username=u;Password=***'
    $result = Initialize-PostgresTestConnection -HomeDirectory $homeForPrecedence
    Assert-True ($env:POSTGRES_TEST_CONNECTION -eq 'Host=localhost;Port=5432;Database=already-set;Username=u;Password=***') "env var wins: an explicitly set POSTGRES_TEST_CONNECTION is left untouched even though a file is present (got '$env:POSTGRES_TEST_CONNECTION')."
    Assert-True ($result.Source -eq 'ExplicitLocal') "env var wins: reports Source 'ExplicitLocal' (got '$($result.Source)')."
}
finally {
    $env:POSTGRES_TEST_CONNECTION = $savedEnv
    Remove-Item -Recurse -Force $homeForPrecedence -ErrorAction SilentlyContinue
}

# ── (e) Initialize-PostgresTestConnection: env var unset, file present, -UseHostedDb passed ─────
# TASK-0078: pre-TASK-0078 this was the DEFAULT (no switch needed); now the switch is required --
# see (h) below for the new default (file ignored without it). Proven to fail against the
# pre-change resolver the same way as (d): -UseHostedDb does not exist there.
$homeForResolution = New-FixtureHome -Content $marker
$savedEnv = $env:POSTGRES_TEST_CONNECTION
try {
    $env:POSTGRES_TEST_CONNECTION = $null
    Initialize-PostgresTestConnection -HomeDirectory $homeForResolution -UseHostedDb | Out-Null
    Assert-True ($env:POSTGRES_TEST_CONNECTION -eq $marker) "env var unset, file present, -UseHostedDb: POSTGRES_TEST_CONNECTION is set from the file, trimmed (got '$env:POSTGRES_TEST_CONNECTION')."
}
finally {
    $env:POSTGRES_TEST_CONNECTION = $savedEnv
    Remove-Item -Recurse -Force $homeForResolution -ErrorAction SilentlyContinue
}

# ── (f) Initialize-PostgresTestConnection: env var unset, file absent -> stays unset, no throw ───
$homeForAbsence = New-FixtureHome -Content $null
$savedEnv = $env:POSTGRES_TEST_CONNECTION
try {
    $env:POSTGRES_TEST_CONNECTION = $null
    Initialize-PostgresTestConnection -HomeDirectory $homeForAbsence | Out-Null
    Assert-True ([string]::IsNullOrEmpty($env:POSTGRES_TEST_CONNECTION)) "env var unset, file absent: POSTGRES_TEST_CONNECTION stays unset (got '$env:POSTGRES_TEST_CONNECTION'), and no exception was thrown to reach this line."
}
finally {
    $env:POSTGRES_TEST_CONNECTION = $savedEnv
    Remove-Item -Recurse -Force $homeForAbsence -ErrorAction SilentlyContinue
}

# ── (g) never echoed: capture every stream, with -Verbose, and grep for the marker ──────────────
# TASK-0078: -UseHostedDb is required now to reach the file-read path at all; unrelated to what
# this assertion is proving (that the diagnostic never echoes the value), so it is simply added.
$homeForEcho = New-FixtureHome -Content $marker
$savedEnv = $env:POSTGRES_TEST_CONNECTION
try {
    $env:POSTGRES_TEST_CONNECTION = $null
    # TASK-0078: Initialize-PostgresTestConnection now RETURNS a [pscustomobject] (the Source
    # result), which is itself a success-stream value -- Out-Null inside the block discards just
    # that, while 4>&1 (verbose) and 6>&1 (the Write-Host diagnostic's information stream) still
    # merge out to be captured below, same coverage as before this object was introduced.
    $captured = & { Initialize-PostgresTestConnection -HomeDirectory $homeForEcho -UseHostedDb -Verbose | Out-Null } 4>&1 6>&1 | Out-String
    Assert-True ($env:POSTGRES_TEST_CONNECTION -eq $marker) 'never-echoed check: resolution still succeeded under -Verbose (precondition for this assertion to mean anything).'
    Assert-True (-not $captured.Contains('selftest-marker-3fae1c')) "never echoed: no captured stream (stdout, information, or verbose) contains the marker string. Captured:`n$captured"
    Assert-True (-not $captured.Contains($marker)) "never echoed: no captured stream contains the full connection string. Captured:`n$captured"
    Assert-True ($captured -match [regex]::Escape($homeForEcho)) 'never echoed: the diagnostic DOES name the file path (proving the message is not simply absent, which would make the assertion above vacuous).'
}
finally {
    $env:POSTGRES_TEST_CONNECTION = $savedEnv
    Remove-Item -Recurse -Force $homeForEcho -ErrorAction SilentlyContinue
}

# ── TASK-0078: local container by default, hosted database only on explicit -UseHostedDb ────────
# Get-PostgresConnectionHost / Test-IsLocalPostgresHost are pure -- exercised directly first, since
# every scenario below depends on them classifying a host correctly.

Assert-True ((Get-PostgresConnectionHost -ConnectionString 'Host=localhost;Port=5432;Database=d;Username=u;Password=***') -eq 'localhost') 'Get-PostgresConnectionHost: reads the Host= key.'
Assert-True ((Get-PostgresConnectionHost -ConnectionString 'Server=db.neon.tech;Port=5432') -eq 'db.neon.tech') 'Get-PostgresConnectionHost: reads the Server= key when Host= is absent.'
Assert-True ($null -eq (Get-PostgresConnectionHost -ConnectionString 'Port=5432;Database=d')) 'Get-PostgresConnectionHost: returns $null when neither Host= nor Server= is present.'
Assert-True ($null -eq (Get-PostgresConnectionHost -ConnectionString '')) 'Get-PostgresConnectionHost: returns $null for an empty string, does not throw.'
Assert-True ($null -eq (Get-PostgresConnectionHost -ConnectionString $null)) 'Get-PostgresConnectionHost: returns $null for $null, does not throw.'

Assert-True (Test-IsLocalPostgresHost -HostName 'localhost') 'Test-IsLocalPostgresHost: localhost is local.'
Assert-True (Test-IsLocalPostgresHost -HostName 'LOCALHOST') 'Test-IsLocalPostgresHost: case-insensitive.'
Assert-True (Test-IsLocalPostgresHost -HostName '127.0.0.1') 'Test-IsLocalPostgresHost: 127.0.0.1 is local.'
Assert-True (Test-IsLocalPostgresHost -HostName '::1') 'Test-IsLocalPostgresHost: ::1 is local.'
Assert-True (Test-IsLocalPostgresHost -HostName '[::1]') 'Test-IsLocalPostgresHost: bracketed ::1 is local.'
Assert-True (-not (Test-IsLocalPostgresHost -HostName 'db.neon.tech')) 'Test-IsLocalPostgresHost: a hosted host is not local.'
Assert-True (-not (Test-IsLocalPostgresHost -HostName $null)) 'Test-IsLocalPostgresHost: $null is not local (fails closed).'
Assert-True (-not (Test-IsLocalPostgresHost -HostName '')) 'Test-IsLocalPostgresHost: empty string is not local (fails closed).'

# ── (h) file present, no -UseHostedDb: ignored entirely, POSTGRES_TEST_CONNECTION stays unset ───
$homeFileOnly = New-FixtureHome -Content $marker
$savedEnv = $env:POSTGRES_TEST_CONNECTION
try {
    $env:POSTGRES_TEST_CONNECTION = $null
    $result = Initialize-PostgresTestConnection -HomeDirectory $homeFileOnly
    Assert-True ($result.Source -eq 'Unset') "no -UseHostedDb: reports Source 'Unset' even though the file has content (got '$($result.Source)')."
    Assert-True ([string]::IsNullOrEmpty($env:POSTGRES_TEST_CONNECTION)) "no -UseHostedDb: POSTGRES_TEST_CONNECTION stays unset although the file is present and non-empty (got '$env:POSTGRES_TEST_CONNECTION')."
}
finally {
    $env:POSTGRES_TEST_CONNECTION = $savedEnv
    Remove-Item -Recurse -Force $homeFileOnly -ErrorAction SilentlyContinue
}

# ── (i) file present, -UseHostedDb passed: read and used, same as the old default behaviour ─────
$homeFileUsed = New-FixtureHome -Content $marker
$savedEnv = $env:POSTGRES_TEST_CONNECTION
try {
    $env:POSTGRES_TEST_CONNECTION = $null
    $result = Initialize-PostgresTestConnection -HomeDirectory $homeFileUsed -UseHostedDb
    Assert-True ($result.Source -eq 'HostedFile') "-UseHostedDb passed: reports Source 'HostedFile' (got '$($result.Source)')."
    Assert-True ($env:POSTGRES_TEST_CONNECTION -eq $marker) "-UseHostedDb passed: POSTGRES_TEST_CONNECTION is set from the file (got '$env:POSTGRES_TEST_CONNECTION')."
}
finally {
    $env:POSTGRES_TEST_CONNECTION = $savedEnv
    Remove-Item -Recurse -Force $homeFileUsed -ErrorAction SilentlyContinue
}

# ── (j) -UseHostedDb passed but the file is missing: falls through to 'Unset', no throw ─────────
$homeFileMissing = New-FixtureHome -Content $null
$savedEnv = $env:POSTGRES_TEST_CONNECTION
try {
    $env:POSTGRES_TEST_CONNECTION = $null
    $result = Initialize-PostgresTestConnection -HomeDirectory $homeFileMissing -UseHostedDb
    Assert-True ($result.Source -eq 'Unset') "-UseHostedDb passed, file missing: reports Source 'Unset' (got '$($result.Source)'), does not throw."
    Assert-True ([string]::IsNullOrEmpty($env:POSTGRES_TEST_CONNECTION)) '-UseHostedDb passed, file missing: POSTGRES_TEST_CONNECTION stays unset.'
}
finally {
    $env:POSTGRES_TEST_CONNECTION = $savedEnv
    Remove-Item -Recurse -Force $homeFileMissing -ErrorAction SilentlyContinue
}

# ── (k) explicit env var, CI: always wins, no host check, regardless of -UseHostedDb ────────────
$homeForCi = New-FixtureHome -Content $null
$savedEnv = $env:POSTGRES_TEST_CONNECTION
try {
    $env:POSTGRES_TEST_CONNECTION = 'Host=db.neon.tech;Port=5432;Database=d;Username=u;Password=***'
    $result = Initialize-PostgresTestConnection -HomeDirectory $homeForCi -IsCi
    Assert-True ($result.Source -eq 'ExplicitCi') "CI, non-local explicit env var: reports Source 'ExplicitCi' (got '$($result.Source)')."
    Assert-True ($env:POSTGRES_TEST_CONNECTION -eq 'Host=db.neon.tech;Port=5432;Database=d;Username=u;Password=***') 'CI: the explicit value is left untouched.'
}
finally {
    $env:POSTGRES_TEST_CONNECTION = $savedEnv
    Remove-Item -Recurse -Force $homeForCi -ErrorAction SilentlyContinue
}

# ── (l) explicit env var, outside CI, LOCAL host: allowed without -UseHostedDb ───────────────────
$homeForLocalExplicit = New-FixtureHome -Content $null
$savedEnv = $env:POSTGRES_TEST_CONNECTION
try {
    $env:POSTGRES_TEST_CONNECTION = 'Host=localhost;Port=5432;Database=d;Username=u;Password=***'
    $result = Initialize-PostgresTestConnection -HomeDirectory $homeForLocalExplicit
    Assert-True ($result.Source -eq 'ExplicitLocal') "non-CI, local explicit env var, no -UseHostedDb: reports Source 'ExplicitLocal' (got '$($result.Source)'), does not throw."
}
finally {
    $env:POSTGRES_TEST_CONNECTION = $savedEnv
    Remove-Item -Recurse -Force $homeForLocalExplicit -ErrorAction SilentlyContinue
}

# ── (m) explicit env var, outside CI, NON-local host, no -UseHostedDb: REJECTED ──────────────────
# The core of TASK-0078: a hosted connection must never arrive by an ambient environment variable.
$homeForRejection = New-FixtureHome -Content $null
$savedEnv = $env:POSTGRES_TEST_CONNECTION
try {
    $env:POSTGRES_TEST_CONNECTION = 'Host=db.neon.tech;Port=5432;Database=d;Username=u;Password=***'
    $threw = $false
    $thrownMessage = $null
    try {
        Initialize-PostgresTestConnection -HomeDirectory $homeForRejection | Out-Null
    }
    catch {
        $threw = $true
        $thrownMessage = $_.Exception.Message
    }
    Assert-True $threw 'non-CI, non-local explicit env var, no -UseHostedDb: Initialize-PostgresTestConnection throws rather than silently using it.'
    Assert-True ($null -ne $thrownMessage -and $thrownMessage -notmatch [regex]::Escape('db.neon.tech;Port=5432;Database=d;Username=u;Password=***')) "rejection message never contains the connection string password/full value (got: $thrownMessage)"
    Assert-True ($null -ne $thrownMessage -and $thrownMessage -match '-UseHostedDb') 'rejection message names the way out (-UseHostedDb).'
}
finally {
    $env:POSTGRES_TEST_CONNECTION = $savedEnv
    Remove-Item -Recurse -Force $homeForRejection -ErrorAction SilentlyContinue
}

# ── (n) same non-local env var, outside CI, WITH -UseHostedDb: now allowed ───────────────────────
$homeForConfirmedHosted = New-FixtureHome -Content $null
$savedEnv = $env:POSTGRES_TEST_CONNECTION
try {
    $env:POSTGRES_TEST_CONNECTION = 'Host=db.neon.tech;Port=5432;Database=d;Username=u;Password=***'
    $result = Initialize-PostgresTestConnection -HomeDirectory $homeForConfirmedHosted -UseHostedDb
    Assert-True ($result.Source -eq 'ExplicitHosted') "non-CI, non-local explicit env var, WITH -UseHostedDb: reports Source 'ExplicitHosted' (got '$($result.Source)'), does not throw."
    Assert-True ($env:POSTGRES_TEST_CONNECTION -eq 'Host=db.neon.tech;Port=5432;Database=d;Username=u;Password=***') '-UseHostedDb: the explicit value is left untouched (not overwritten from a file).'
}
finally {
    $env:POSTGRES_TEST_CONNECTION = $savedEnv
    Remove-Item -Recurse -Force $homeForConfirmedHosted -ErrorAction SilentlyContinue
}

# ── TASK-0078 (orchestrator ruling, 2026-09-17): Get-IntegrationFailureGuidance ──────────────────
# Replaces the withdrawn pre-flight-probe AC. Only the local-container source ('Unset') gets
# guidance; every other source -- a database a human explicitly chose one way or another -- gets
# $null, so ci.ps1 prints nothing extra when the failure has nothing to do with the container path.
$expectedGuidance = "If the local container failed to start, do NOT fall back to the hosted " +
    "database without the human's confirmation. Ask them to check the WSL Docker daemon; re-run " +
    "with -UseHostedDb only after they confirm."

$guidanceUnset = Get-IntegrationFailureGuidance -Source 'Unset'
Assert-True ($guidanceUnset -eq $expectedGuidance) "Get-IntegrationFailureGuidance('Unset'): returns the exact guidance text (got '$guidanceUnset')."

foreach ($nonContainerSource in @('ExplicitCi', 'ExplicitLocal', 'ExplicitHosted', 'HostedFile')) {
    $guidance = Get-IntegrationFailureGuidance -Source $nonContainerSource
    Assert-True ($null -eq $guidance) "Get-IntegrationFailureGuidance('$nonContainerSource'): returns `$null (got '$guidance') -- a database the human already chose gets no extra steer."
}

Write-Host ''
if ($script:failures.Count -gt 0) {
    Write-Host "FAILED: $($script:failures.Count) of $script:assertionCount assertion(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host "PASSED: $script:assertionCount assertion(s)." -ForegroundColor Green
exit 0
