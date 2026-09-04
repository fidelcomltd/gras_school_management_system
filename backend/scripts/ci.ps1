<#
.SYNOPSIS
    Runs every quality gate. The SAME script CI runs, so "it passes on my machine" means something.

.DESCRIPTION
    Gates, in order (cheapest and most likely to fail first, so feedback is fast):

      1.  restore
      2.  dotnet format --verify-no-changes
      3.  build with warnings as errors
      4.  generate the OpenAPI document (no database — SchoolManagement.ArchitectureTests reads it)
      5.  unit + architecture tests, with coverage collection
      6.  integration tests, with coverage collection
      7.  coverage threshold (also verdicts the test run itself — see -AllowSkipped below)
      8.  dependency vulnerability scan
      9.  secret scan (skipped with a warning if gitleaks is not installed)
      10. OpenAPI contract drift

    By default the script stops at the FIRST failing gate: a contributor fixes one thing, pushes,
    and finds out about the next thing rather than waiting through a database round trip to learn a
    typecheck was broken all along. Pass -NoFailFast to run every gate regardless and summarise at
    the end instead — this is what CI wants, since it would rather see the full picture in one run
    than re-trigger per fix.

.PARAMETER Configuration
    Build configuration. Defaults to Release, matching CI.

.PARAMETER CoverageThreshold
    Minimum line coverage percentage. A FLOOR, NOT A GOAL — see the note where it is checked.

.PARAMETER SkipContractDrift
    Skip the OpenAPI drift check. For the rare case where the contract is being changed deliberately
    in the same commit and has not been promoted yet.

.PARAMETER AllowSkipped
    Do not fail the run when the test suite reports skipped/not-executed tests (for example,
    integration tests skipping because POSTGRES_TEST_CONNECTION is not set). The skip is still
    reported, by suite name, and the coverage floor is still not enforced against an incomplete
    run — only whether a skip fails the SCRIPT changes. Without this switch, a skipped suite is a
    non-zero exit (CLAUDE.md §13: a skipped suite is not a passing suite).

.PARAMETER NoFailFast
    Run every gate even after one fails, then exit non-zero with a summary, instead of stopping at
    the first failure. This is the CI workflow's mode.

.EXAMPLE
    ./scripts/ci.ps1

.EXAMPLE
    ./scripts/ci.ps1 -NoFailFast -AllowSkipped
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateRange(0, 100)]
    [int]$CoverageThreshold = 60,

    [switch]$SkipContractDrift,

    [switch]$AllowSkipped,

    [switch]$NoFailFast
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

$backendRoot = Split-Path -Parent $PSScriptRoot
Push-Location $backendRoot

# One implementation of "what does this batch of .trx files mean" — shared with the self-test under
# backend/scripts/tests, which exercises these same functions against fixture .trx files.
. (Join-Path $PSScriptRoot 'lib/gate-summary.ps1')

$failures = [System.Collections.Generic.List[string]]::new()
$gateVerdicts = [System.Collections.Generic.List[string]]::new()
$script:testCountsLine = $null
$script:coverageLine = $null

function Write-FinalSummary {
    # A FIXED block: one line per gate that ran, plus the test and coverage numbers when they were
    # produced. Pasting this block alone is meant to satisfy CLAUDE.md §9 — no scrolling the log.
    Write-Host ''
    Write-Host '════════════════════════════════════════════════' -ForegroundColor Cyan
    Write-Host 'SUMMARY' -ForegroundColor Cyan
    foreach ($line in $gateVerdicts) {
        Write-Host "  $line"
    }
    if ($script:testCountsLine) {
        Write-Host "  $($script:testCountsLine)"
    }
    if ($script:coverageLine) {
        Write-Host "  $($script:coverageLine)"
    }
    Write-Host ''

    if ($failures.Count -gt 0) {
        Write-Host "FAILED GATES ($($failures.Count)):" -ForegroundColor Red
        $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    }
    else {
        Write-Host 'ALL GATES PASSED' -ForegroundColor Green
    }
}

function Invoke-Gate {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Action
    )

    Write-Host ''
    Write-Host "══════ $Name ══════" -ForegroundColor Cyan

    try {
        & $Action
        if ($LASTEXITCODE -ne 0) {
            throw "exit code $LASTEXITCODE"
        }
        Write-Host "PASS: $Name" -ForegroundColor Green
        $gateVerdicts.Add("PASS: $Name")
    }
    catch {
        Write-Host "FAIL: $Name -- $($_.Exception.Message)" -ForegroundColor Red
        $failures.Add($Name)
        $gateVerdicts.Add("FAIL: $Name -- $($_.Exception.Message)")

        if (-not $NoFailFast) {
            # Cheapest-first ordering only pays off if a failure actually stops the run here. The
            # `finally` block below still runs (Pop-Location) because `exit` unwinds through it.
            Write-FinalSummary
            exit 1
        }
    }
}

try {
    Invoke-Gate 'Restore' { dotnet restore --nologo }

    Invoke-Gate 'Format' {
        # No build needed first: `dotnet format` only needs the restored package graph, not built
        # output, so it can run ahead of Build per the cheapest-first ordering.
        dotnet format --verify-no-changes --no-restore
    }

    Invoke-Gate 'Build (warnings as errors)' {
        # TreatWarningsAsErrors is already set in Directory.Build.props; passing it again makes the
        # intent explicit at the call site and survives someone loosening the props file.
        dotnet build --no-restore --configuration $Configuration --nologo -warnaserror
    }

    Invoke-Gate 'Generate OpenAPI document (no database)' {
        # SchoolManagement.ArchitectureTests reads this artefact directly instead of starting the
        # application (TASK-0009 moved the document-property contract tests off the database, since
        # none of them ever touched it), so it must exist before the test gates run below. This is
        # the ONLY call to generate-openapi.ps1 in this script: the "OpenAPI contract drift" gate
        # further down reuses the same file rather than regenerating it, so there is exactly one
        # path to this document, not two.
        & (Join-Path $PSScriptRoot 'generate-openapi.ps1') -Configuration $Configuration | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw 'Document generation failed.'
        }
        $global:LASTEXITCODE = 0
    }

    Invoke-Gate 'Unit & architecture tests' {
        # Cleared here, once, before the FIRST test run: stale reports from a previous run would
        # otherwise be merged into this one and report coverage for code that no longer exists. The
        # integration-tests gate below deliberately does NOT clear this directory again — both gates
        # deposit into it so the coverage-threshold gate's ReportGenerator merge sees both.
        Remove-Item -Recurse -Force './artifacts/coverage' -ErrorAction SilentlyContinue

        $projects = @(
            'tests/SchoolManagement.UnitTests/SchoolManagement.UnitTests.csproj'
            'tests/SchoolManagement.ArchitectureTests/SchoolManagement.ArchitectureTests.csproj'
        )

        $exitCode = 0
        foreach ($project in $projects) {
            dotnet test $project --no-build --configuration $Configuration --nologo `
                --settings coverlet.runsettings `
                --results-directory './artifacts/coverage' `
                --logger 'trx'
            if ($LASTEXITCODE -ne 0) {
                $exitCode = $LASTEXITCODE
            }
        }
        $global:LASTEXITCODE = $exitCode
    }

    Invoke-Gate 'Integration tests' {
        # These SKIP when no PostgreSQL is reachable — they never silently pass. To run them here,
        # set POSTGRES_TEST_CONNECTION or make a container runtime available. CI must do one of
        # those, or the suite it is guarding is much smaller than it looks. A skip now fails the
        # script (see the Coverage threshold gate below) unless -AllowSkipped is passed.
        if (-not $env:POSTGRES_TEST_CONNECTION) {
            Write-Host 'NOTE: POSTGRES_TEST_CONNECTION is not set. Integration tests will be SKIPPED' -ForegroundColor Yellow
            Write-Host '      unless a container runtime is available. They are NOT passing — they are absent.' -ForegroundColor Yellow
        }

        dotnet test 'tests/SchoolManagement.IntegrationTests/SchoolManagement.IntegrationTests.csproj' --no-build --configuration $Configuration --nologo `
            --settings coverlet.runsettings `
            --results-directory './artifacts/coverage' `
            --logger 'trx'
    }

    Invoke-Gate "Coverage threshold ($CoverageThreshold%)" {
        $reports = @(Get-ChildItem './artifacts/coverage' -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue)

        if ($reports.Count -eq 0) {
            throw 'No coverage report was produced. Did an earlier test gate fail?'
        }

        # THE REPORTS MUST BE MERGED, NOT SAMPLED. Each test project emits its own cobertura file, and
        # the same production assembly appears in several of them — a line covered only by the
        # integration tests shows as uncovered in the unit-test report. Reading one file (or summing the
        # files naively, which double-counts shared assemblies) produces a number that swings wildly
        # depending on which file was picked. ReportGenerator merges them properly, by class and line.
        Write-Host "Merging $($reports.Count) coverage report(s)..."

        $merged = './artifacts/coverage/merged'
        Remove-Item -Recurse -Force $merged -ErrorAction SilentlyContinue

        # The GUID directories only. The test host also copies each report under
        # <trx-name>/In/<machine>/, so a broader glob feeds ReportGenerator two identical copies of every
        # report — harmless (it merges by line) but slower and confusing in the log.
        dotnet reportgenerator `
            "-reports:./artifacts/coverage/*/coverage.cobertura.xml" `
            "-targetdir:$merged" `
            '-reporttypes:Cobertura;TextSummary' `
            '-verbosity:Warning' | Out-Null

        if ($LASTEXITCODE -ne 0) {
            throw "reportgenerator failed with exit code $LASTEXITCODE."
        }

        $mergedReport = Join-Path $merged 'Cobertura.xml'
        if (-not (Test-Path $mergedReport)) {
            throw "reportgenerator did not produce '$mergedReport'."
        }

        [xml]$coverage = Get-Content $mergedReport -Raw

        # Read via DocumentElement rather than $coverage.coverage: ReportGenerator emits a DOCTYPE
        # declaration also named "coverage", so the dotted accessor resolves to both the doctype node and
        # the root element and the attribute lookup fails on the resulting collection.
        $lineRate = [double]$coverage.DocumentElement.GetAttribute('line-rate') * 100
        $branchRate = [double]$coverage.DocumentElement.GetAttribute('branch-rate') * 100

        Write-Host ("Line coverage:   {0:N2}%" -f $lineRate)
        Write-Host ("Branch coverage: {0:N2}%" -f $branchRate)

        # ── Was the suite actually complete, and did it pass? ─────────────────────────────────────
        # Delegated to gate-summary.ps1 (dot-sourced above), so this exists in exactly one place —
        # the self-test under backend/scripts/tests exercises these same functions against fixture
        # .trx files. Summed across EVERY trx in the directory: one file per test project run above.
        #
        # A FAILED test used to read as a pass here: this gate runs even when an earlier test gate
        # already failed, under -NoFailFast (CI's mode) — printing "PASS: Coverage threshold" over a
        # run with real test failures is exactly the defect this closes
        # (`.agent/drift/2026-Q3.md`, 2026-08-27: 30 integration tests FAILED yet this line printed
        # PASS at 60.48% line coverage). A SKIPPED suite now fails the script too, unless
        # -AllowSkipped is passed — a skipped suite is not a passing suite.
        $testSummary = Get-TrxSummary -ResultsDirectory './artifacts/coverage'
        $verdict = Get-TestRunVerdict -Summary $testSummary -AllowSkipped:$AllowSkipped

        $script:testCountsLine = "Tests: total={0} passed={1} failed={2} skipped={3}" -f `
            $testSummary.Totals.Total, $testSummary.Totals.Passed, $testSummary.Totals.Failed, $testSummary.Totals.Skipped
        $script:coverageLine = "Coverage: line={0:N2}% branch={1:N2}%" -f $lineRate, $branchRate

        foreach ($message in $verdict.Messages) {
            Write-Host $message -ForegroundColor Yellow
        }

        if (-not $verdict.Passed) {
            throw 'Test run was not clean (failed and/or skipped tests) - see the messages above.'
        }

        # THE THRESHOLD IS A FLOOR, NOT A GOAL. It exists to catch a collapse — someone deleting a test
        # project, or a large untested subsystem landing at once. Chasing the number produces tests that
        # execute code without asserting anything about it, which is worse than no test because it looks
        # like cover. Judge a pull request on whether its behaviour is tested, not on whether this moved.
        if ($testSummary.Totals.Skipped -gt 0) {
            # Only reachable with -AllowSkipped (otherwise the verdict above already threw). The
            # floor still must not be judged against an incomplete run — only whether a skip fails
            # the SCRIPT changed, not whether the floor is enforced against a partial run.
            Write-Host ''
            Write-Host 'COVERAGE FLOOR NOT ENFORCED: the suite above was incomplete (see the skip message(s)' -ForegroundColor Yellow
            Write-Host 'above), so this number is not comparable to the floor. The integration tests need' -ForegroundColor Yellow
            Write-Host 'PostgreSQL — set POSTGRES_TEST_CONNECTION or start a container runtime, then drop' -ForegroundColor Yellow
            Write-Host '-AllowSkipped and this gate will measure and enforce properly.' -ForegroundColor Yellow
            $global:LASTEXITCODE = 0
            return
        }

        if ($lineRate -lt $CoverageThreshold) {
            throw ("Line coverage {0:N2}% is below the {1}% floor." -f $lineRate, $CoverageThreshold)
        }
        $global:LASTEXITCODE = 0
    }

    Invoke-Gate 'Vulnerable dependencies' {
        $output = dotnet list package --vulnerable --include-transitive --format json 2>&1 | Out-String

        # `dotnet list package` exits 0 even when it finds advisories, so the OUTPUT has to be inspected.
        # Checking only the exit code here would make this gate permanently green and useless.
        if ($output -match '"severity"') {
            Write-Host $output
            throw 'Vulnerable packages found. Update them, or record an accepted risk in docs/ASSUMPTIONS.md.'
        }

        Write-Host 'No known vulnerable packages.'
        $global:LASTEXITCODE = 0
    }

    Invoke-Gate 'Secret scan' {
        $gitleaks = Get-Command gitleaks -ErrorAction SilentlyContinue

        if (-not $gitleaks) {
            # A warning, not a pass. CI installs gitleaks so this branch is not taken there; locally it
            # means the gate did not run, and saying so is more honest than reporting success.
            Write-Host 'WARNING: gitleaks is not installed, so the secret scan DID NOT RUN.' -ForegroundColor Yellow
            Write-Host '         Install it (https://github.com/gitleaks/gitleaks) for local coverage.' -ForegroundColor Yellow
            $global:LASTEXITCODE = 0
            return
        }

        gitleaks detect --source . --config .gitleaks.toml --redact --no-banner
    }

    if ($SkipContractDrift) {
        Write-Host ''
        Write-Host 'SKIPPED: OpenAPI contract drift (explicitly requested)' -ForegroundColor Yellow
        $gateVerdicts.Add('SKIP: OpenAPI contract drift (explicitly requested)')
    }
    else {
        Invoke-Gate 'OpenAPI contract drift' {
            $committed = Join-Path (Split-Path -Parent $backendRoot) 'contracts/openapi.json'

            if (-not (Test-Path $committed)) {
                throw "No committed contract at '$committed'. Create it with: ./scripts/generate-openapi.ps1 -Promote"
            }

            $generated = Join-Path $backendRoot 'artifacts/openapi/SchoolManagement.Api.json'

            if (-not (Test-Path $generated)) {
                throw "Expected a generated document at '$generated' - the earlier 'Generate OpenAPI document' gate should have produced it. Did that gate fail?"
            }

            # Hash comparison, not a text diff: it is exact, and it cannot be fooled by line endings.
            $generatedHash = (Get-FileHash $generated -Algorithm SHA256).Hash
            $committedHash = (Get-FileHash $committed -Algorithm SHA256).Hash

            if ($generatedHash -ne $committedHash) {
                Write-Host 'The committed contract does not match the code.' -ForegroundColor Red
                Write-Host 'An endpoint changed without the contract being regenerated, so the frontend' -ForegroundColor Red
                Write-Host 'client is generated from a stale document. Fix with:' -ForegroundColor Red
                Write-Host '    ./scripts/generate-openapi.ps1 -Promote' -ForegroundColor Yellow
                Write-Host 'then review the diff — a removed or renamed field is a BREAKING change.' -ForegroundColor Yellow

                # Show the actual difference, so the failure is diagnosable from the CI log alone.
                $diff = Compare-Object (Get-Content $committed) (Get-Content $generated)
                $diff | Select-Object -First 40 | Format-Table -AutoSize | Out-String | Write-Host

                throw 'Contract drift detected.'
            }

            Write-Host 'The committed contract matches the code.'
            $global:LASTEXITCODE = 0
        }
    }

    Write-FinalSummary

    if ($failures.Count -gt 0) {
        exit 1
    }

    exit 0
}
finally {
    Pop-Location
}
