<#
.SYNOPSIS
    Dependency-free self-test for backend/scripts/lib/postgres-test-connection.ps1 (TASK-0031).

.DESCRIPTION
    Same hand-rolled harness style as gate-summary.tests.ps1 / local-env.tests.ps1 — this machine
    has only the ancient Windows-bundled Pester 3.4.0 and no `pwsh`, so this is assertions plus
    Write-Host, not a Pester suite.

    Dot-sources lib/postgres-test-connection.ps1 and calls its two functions IN-PROCESS, against a
    throwaway temp directory standing in for $HOME — never against the real
    $HOME/.gras/pg-test.txt, so this suite cannot read (or leak) a real credential and passes
    identically whether or not this machine happens to have that file.

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
$homeForPrecedence = New-FixtureHome -Content $marker
$savedEnv = $env:POSTGRES_TEST_CONNECTION
try {
    $env:POSTGRES_TEST_CONNECTION = 'already-set-value'
    Initialize-PostgresTestConnection -HomeDirectory $homeForPrecedence
    Assert-True ($env:POSTGRES_TEST_CONNECTION -eq 'already-set-value') "env var wins: an explicitly set POSTGRES_TEST_CONNECTION is left untouched even though a file is present (got '$env:POSTGRES_TEST_CONNECTION')."
}
finally {
    $env:POSTGRES_TEST_CONNECTION = $savedEnv
    Remove-Item -Recurse -Force $homeForPrecedence -ErrorAction SilentlyContinue
}

# ── (e) Initialize-PostgresTestConnection: env var unset, file present -> env var is set from it ──
$homeForResolution = New-FixtureHome -Content $marker
$savedEnv = $env:POSTGRES_TEST_CONNECTION
try {
    $env:POSTGRES_TEST_CONNECTION = $null
    Initialize-PostgresTestConnection -HomeDirectory $homeForResolution
    Assert-True ($env:POSTGRES_TEST_CONNECTION -eq $marker) "env var unset, file present: POSTGRES_TEST_CONNECTION is set from the file, trimmed (got '$env:POSTGRES_TEST_CONNECTION')."
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
    Initialize-PostgresTestConnection -HomeDirectory $homeForAbsence
    Assert-True ([string]::IsNullOrEmpty($env:POSTGRES_TEST_CONNECTION)) "env var unset, file absent: POSTGRES_TEST_CONNECTION stays unset (got '$env:POSTGRES_TEST_CONNECTION'), and no exception was thrown to reach this line."
}
finally {
    $env:POSTGRES_TEST_CONNECTION = $savedEnv
    Remove-Item -Recurse -Force $homeForAbsence -ErrorAction SilentlyContinue
}

# ── (g) never echoed: capture every stream, with -Verbose, and grep for the marker ──────────────
$homeForEcho = New-FixtureHome -Content $marker
$savedEnv = $env:POSTGRES_TEST_CONNECTION
try {
    $env:POSTGRES_TEST_CONNECTION = $null
    $captured = Initialize-PostgresTestConnection -HomeDirectory $homeForEcho -Verbose *>&1 | Out-String
    Assert-True ($env:POSTGRES_TEST_CONNECTION -eq $marker) 'never-echoed check: resolution still succeeded under -Verbose (precondition for this assertion to mean anything).'
    Assert-True (-not $captured.Contains('selftest-marker-3fae1c')) "never echoed: no captured stream (stdout, information, or verbose) contains the marker string. Captured:`n$captured"
    Assert-True (-not $captured.Contains($marker)) "never echoed: no captured stream contains the full connection string. Captured:`n$captured"
    Assert-True ($captured -match [regex]::Escape($homeForEcho)) 'never echoed: the diagnostic DOES name the file path (proving the message is not simply absent, which would make the assertion above vacuous).'
}
finally {
    $env:POSTGRES_TEST_CONNECTION = $savedEnv
    Remove-Item -Recurse -Force $homeForEcho -ErrorAction SilentlyContinue
}

Write-Host ''
if ($script:failures.Count -gt 0) {
    Write-Host "FAILED: $($script:failures.Count) of $script:assertionCount assertion(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host "PASSED: $script:assertionCount assertion(s)." -ForegroundColor Green
exit 0
