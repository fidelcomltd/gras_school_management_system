<#
.SYNOPSIS
    Dependency-free self-test for backend/scripts/lib/gate-summary.ps1.

.DESCRIPTION
    This machine has only the ancient Windows-bundled Pester 3.4.0 and no `pwsh`, so this is a
    hand-rolled assertion harness, not a Pester suite — no `Install-Module`, no new dependency.
    It invokes gate-summary.ps1 in a CHILD PROCESS for every case (via `powershell.exe -File`, so
    it works the same way a contributor or CI running the real script would) and asserts on the
    ACTUAL process exit code — a dot-sourced call has no exit code of its own to observe, so that
    path alone would not prove anything about the `exit 0` / `exit 1` at the bottom of the file.

    Fixture directories (backend/scripts/tests/fixtures/), each holding one hand-authored .trx:

      happy/   - all passed.                          Must exit 0.
      failed/  - the 2026-08-27 run this task closes:  30 integration tests FAILED at 60.48% line
                 coverage, yet ci.ps1 printed PASS. Must exit non-zero, -AllowSkipped or not — a
                 failure is not a skip and -AllowSkipped must not paper over one.
      skipped/ - the real xunit v3 shape: total=31, passed=0, failed=0, notExecuted=0 — dynamic
                 skips (Assert.Skip) do not increment notExecuted, which is why Get-TrxSummary
                 derives Skipped as total - passed - failed instead of trusting that counter. Must
                 exit non-zero without -AllowSkipped, and 0 with it.

    Also dot-sources gate-summary.ps1 to call Get-TrxSummary / Get-TestRunVerdict directly, since
    that in-process path is how ci.ps1 itself uses this file, not the script-mode exit path.

    Exits 0 if every assertion passed, 1 otherwise (each failing assertion printed).

.EXAMPLE
    ./scripts/tests/gate-summary.tests.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$testsRoot = $PSScriptRoot
$scriptsRoot = Split-Path -Parent $testsRoot
$gateSummary = Join-Path $scriptsRoot 'lib/gate-summary.ps1'
$fixtures = Join-Path $testsRoot 'fixtures'

if (-not (Test-Path -LiteralPath $gateSummary)) {
    Write-Host "FAIL: cannot find gate-summary.ps1 at '$gateSummary'." -ForegroundColor Red
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

function Invoke-GateSummaryProcess {
    # Runs gate-summary.ps1 as its OWN process, not dot-sourced, so $LASTEXITCODE below reflects
    # the real `exit 0` / `exit 1` in the script — exactly what a contributor or CI observes.
    param(
        [Parameter(Mandatory)][string]$ResultsDirectory,
        [switch]$AllowSkipped
    )

    $arguments = @('-NoProfile', '-NonInteractive', '-File', $gateSummary, '-ResultsDirectory', $ResultsDirectory)
    if ($AllowSkipped) {
        $arguments += '-AllowSkipped'
    }

    $output = & powershell.exe @arguments 2>&1 | Out-String
    [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output   = $output
    }
}

# ── (a) happy path: all passed ──────────────────────────────────────────────────────────────────
$happy = Invoke-GateSummaryProcess -ResultsDirectory (Join-Path $fixtures 'happy')
Assert-True ($happy.ExitCode -eq 0) "happy fixture: exit code is 0 (got $($happy.ExitCode)). Output:`n$($happy.Output)"

# ── (b) the 2026-08-27 failure run: 30 FAILED, must not read as a pass ──────────────────────────
$failed = Invoke-GateSummaryProcess -ResultsDirectory (Join-Path $fixtures 'failed')
Assert-True ($failed.ExitCode -ne 0) "failed fixture (30 FAILED): exit code is non-zero (got $($failed.ExitCode)). Output:`n$($failed.Output)"
Assert-True ($failed.Output -match 'FAILED') "failed fixture: output mentions the failure. Output:`n$($failed.Output)"

$failedAllowSkipped = Invoke-GateSummaryProcess -ResultsDirectory (Join-Path $fixtures 'failed') -AllowSkipped
Assert-True ($failedAllowSkipped.ExitCode -ne 0) "failed fixture: still non-zero even with -AllowSkipped (a failure is not a skip). Output:`n$($failedAllowSkipped.Output)"

# ── (c) the real xunit v3 skip shape: total=31 passed=0 failed=0 notExecuted=0 ──────────────────
$skipped = Invoke-GateSummaryProcess -ResultsDirectory (Join-Path $fixtures 'skipped')
Assert-True ($skipped.ExitCode -ne 0) "skipped fixture: exit code is non-zero without -AllowSkipped (got $($skipped.ExitCode)). Output:`n$($skipped.Output)"
Assert-True ($skipped.Output -match 'SKIPPED') "skipped fixture: output names the skipped suite. Output:`n$($skipped.Output)"

# The fixture's trx filename (a realistic <user>_<machine>_<timestamp>.trx, carrying no project
# name at all) must NOT be what the message prints — a real trx filename never names a project, so
# a skip line naming the filename tells nobody which of several test projects skipped. It must
# instead name the project derived from inside the file (TestDefinitions/UnitTest/storage).
$skippedTrxName = (Get-ChildItem (Join-Path $fixtures 'skipped') -Filter '*.trx').Name
Assert-True ($skipped.Output -match 'schoolmanagement\.integrationtests') "skipped fixture: SKIPPED line names the project (schoolmanagement.integrationtests), derived from storage, not the trx filename. Output:`n$($skipped.Output)"
Assert-True (-not $skipped.Output.Contains($skippedTrxName)) "skipped fixture: SKIPPED line does NOT contain the trx filename ($skippedTrxName) - that would just be a machine/user/timestamp, not a suite name. Output:`n$($skipped.Output)"

$skippedAllowed = Invoke-GateSummaryProcess -ResultsDirectory (Join-Path $fixtures 'skipped') -AllowSkipped
Assert-True ($skippedAllowed.ExitCode -eq 0) "skipped fixture: exit code is 0 with -AllowSkipped (got $($skippedAllowed.ExitCode)). Output:`n$($skippedAllowed.Output)"

# ── Also exercise the functions in-process (dot-sourced), since that is how ci.ps1 uses them ────
. $gateSummary

$happySummary = Get-TrxSummary -ResultsDirectory (Join-Path $fixtures 'happy')
Assert-True ($happySummary.Totals.Failed -eq 0) 'in-process: happy fixture derives Failed=0.'
Assert-True ($happySummary.Totals.Skipped -eq 0) 'in-process: happy fixture derives Skipped=0.'
$happyVerdict = Get-TestRunVerdict -Summary $happySummary
Assert-True ($happyVerdict.Passed) 'in-process: Get-TestRunVerdict passes an all-passed run.'

$failedSummary = Get-TrxSummary -ResultsDirectory (Join-Path $fixtures 'failed')
Assert-True ($failedSummary.Totals.Failed -eq 30) "in-process: failed fixture derives Failed=30 (got $($failedSummary.Totals.Failed))."
$failedVerdict = Get-TestRunVerdict -Summary $failedSummary
Assert-True (-not $failedVerdict.Passed) 'in-process: Get-TestRunVerdict fails a run with Failed > 0.'

$skippedSummary = Get-TrxSummary -ResultsDirectory (Join-Path $fixtures 'skipped')
Assert-True ($skippedSummary.Totals.Skipped -eq 31) "in-process: skipped fixture derives Skipped=31 from total-passed-failed, not notExecuted (got $($skippedSummary.Totals.Skipped))."
Assert-True ($skippedSummary.Files[0].Suite -eq 'schoolmanagement.integrationtests') "in-process: Get-TrxSummary derives Suite='schoolmanagement.integrationtests' from storage (got '$($skippedSummary.Files[0].Suite)')."
Assert-True ($skippedSummary.Files[0].Suite -ne $skippedSummary.Files[0].Name) 'in-process: derived Suite is not just the trx filename.'
$skippedVerdictDefault = Get-TestRunVerdict -Summary $skippedSummary
Assert-True (-not $skippedVerdictDefault.Passed) 'in-process: Get-TestRunVerdict fails a skipped run by default.'
$skippedVerdictAllowed = Get-TestRunVerdict -Summary $skippedSummary -AllowSkipped
Assert-True ($skippedVerdictAllowed.Passed) 'in-process: Get-TestRunVerdict passes a skipped run with -AllowSkipped.'

Write-Host ''
if ($script:failures.Count -gt 0) {
    Write-Host "FAILED: $($script:failures.Count) of $script:assertionCount assertion(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host "PASSED: $script:assertionCount assertion(s)." -ForegroundColor Green
exit 0
