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

    POSTGRES_TEST_CONNECTION (needed by gate 6, integration tests) is resolved BY THIS SCRIPT
    (TASK-0031) before any gate runs: an explicitly set environment variable wins if present,
    otherwise $HOME/.gras/pg-test.txt is read (BOM-stripped, trimmed) — see
    lib/postgres-test-connection.ps1. Nothing else needs to be set first; the canonical invocation
    is exactly `./scripts/ci.ps1 -NoFailFast`, no environment prelude. A missing file is not fatal —
    the integration-tests gate below still just skips, and says so.

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

    This is UNCHANGED by -SkipIntegration / -IntegrationFilter below, and answers a different
    question: -AllowSkipped is about a test that ran and reported itself skipped (e.g. it could not
    reach Postgres); -SkipIntegration/-IntegrationFilter are about the ORCHESTRATOR choosing not to
    run (all of, or part of) the integration stage at all. The two must never read alike in the
    SUMMARY — see TASK-0067.

.PARAMETER SkipIntegration
    (TASK-0067) Skip the integration-tests gate entirely — gates 1-5 and 8-10 still run in full.
    The SUMMARY reports it as `SKIP: Integration tests (SKIPPED BY REQUEST ...)`, never as a pass.
    Because the run is then necessarily partial, the coverage-threshold gate reports its floor as
    NOT APPLICABLE rather than PASS or FAIL — a partial run's coverage number is meaningless, but
    that is not the same as the gate failing. Mutually exclusive with -IntegrationFilter. This is
    the scoped local gate from `.agent/rules/gates.md` §0 — CI (`backend-ci.yml`) always runs the
    full integration stage and passes neither this nor -IntegrationFilter.

.PARAMETER IntegrationFilter
    (TASK-0067) Run only the integration tests matching this `dotnet test --filter` expression,
    leaving every other gate untouched and full. Goes through THIS script's own
    POSTGRES_TEST_CONNECTION resolution (see above) exactly like the unfiltered run does — a raw,
    hand-rolled `dotnet test --filter` outside this script does NOT resolve that variable, which is
    precisely how TASK-0063 got `Passed: 0, Skipped: 1` and exit 0 on a test that never really ran.

    A filter that matches ZERO tests is treated as a FAILURE of this gate, not a clean pass: a
    typo'd expression reporting green is the same false-green class -SkipIntegration's honest
    labelling exists to kill. Like -SkipIntegration, this makes the run partial, so the
    coverage-threshold gate reports NOT APPLICABLE rather than enforcing the floor. Mutually
    exclusive with -SkipIntegration.

.PARAMETER NoFailFast
    Run every gate even after one fails, then exit non-zero with a summary, instead of stopping at
    the first failure. This is the CI workflow's mode.

.EXAMPLE
    ./scripts/ci.ps1

.EXAMPLE
    ./scripts/ci.ps1 -NoFailFast -AllowSkipped

.EXAMPLE
    ./scripts/ci.ps1 -SkipIntegration

.EXAMPLE
    ./scripts/ci.ps1 -IntegrationFilter 'FullyQualifiedName~AdmissionApproval'
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateRange(0, 100)]
    [int]$CoverageThreshold = 60,

    [switch]$SkipContractDrift,

    [switch]$AllowSkipped,

    [switch]$NoFailFast,

    # TASK-0067: the scoped local gate. Neither flag changes which gates exist, their order, or
    # their thresholds -- they change whether/how much of gate 6 runs, and gate 7 (coverage) reacts
    # by reporting N/A instead of enforcing a floor against a partial run. See the PARAMETER blocks
    # above for the full rationale.
    [switch]$SkipIntegration,

    [string]$IntegrationFilter
)

if ($SkipIntegration -and $IntegrationFilter) {
    Write-Host 'ERROR: -SkipIntegration and -IntegrationFilter are mutually exclusive -- pick one.' -ForegroundColor Red
    exit 1
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

$backendRoot = Split-Path -Parent $PSScriptRoot
Push-Location $backendRoot

# One implementation of "what does this batch of .trx files mean" — shared with the self-test under
# backend/scripts/tests, which exercises these same functions against fixture .trx files.
. (Join-Path $PSScriptRoot 'lib/gate-summary.ps1')

# Same pattern: one implementation of "how POSTGRES_TEST_CONNECTION is resolved", shared with
# backend/scripts/tests/postgres-test-connection.tests.ps1 (TASK-0031). Resolving it here, once,
# before any gate runs, is what lets every dispatch invoke this whole script as
# `./backend/scripts/ci.ps1 -NoFailFast` with no environment prelude — see STATE.md ## Gate
# commands for why that matters.
. (Join-Path $PSScriptRoot 'lib/postgres-test-connection.ps1')
Initialize-PostgresTestConnection -HomeDirectory $HOME

$failures = [System.Collections.Generic.List[string]]::new()
$gateVerdicts = [System.Collections.Generic.List[string]]::new()
$script:testCountsLine = $null
$script:coverageLine = $null

# TASK-0067: true the moment ANY part of the integration stage is scoped by request -- whether the
# whole stage was skipped (-SkipIntegration) or narrowed to a filter (-IntegrationFilter). This is
# what tells the Coverage threshold gate its floor is not comparable to a partial run and must be
# reported N/A rather than PASS or FAIL. It is deliberately independent of -AllowSkipped, which
# answers a different question (see that parameter's doc comment above).
$script:integrationScoped = [bool]$SkipIntegration -or [bool]$IntegrationFilter
$script:coverageGateNotApplicable = $false

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

    if ($SkipIntegration) {
        # TASK-0067: bypass Invoke-Gate entirely, same shape as -SkipContractDrift below -- this is
        # a stage skipped BY REQUEST, which must never be printed or counted as a pass. It must also
        # never be conflated with a test reporting itself skipped (e.g. Postgres unreachable), which
        # is a different thing entirely and still fails the run below regardless of this flag.
        Write-Host ''
        Write-Host 'SKIPPED: Integration tests (SKIPPED BY REQUEST via -SkipIntegration)' -ForegroundColor Yellow
        $gateVerdicts.Add('SKIP: Integration tests (SKIPPED BY REQUEST via -SkipIntegration)')
    }
    else {
        Invoke-Gate 'Integration tests' {
            # These SKIP when no PostgreSQL is reachable — they never silently pass. POSTGRES_TEST_
            # CONNECTION was already resolved at the top of this script (an explicit env var, or
            # $HOME/.gras/pg-test.txt); if neither existed, it is still unset here and this suite skips,
            # unless a container runtime is available instead. CI must supply one of those, or the suite
            # it is guarding is much smaller than it looks. A skip now fails the script (see the
            # Coverage threshold gate below) unless -AllowSkipped is passed.
            if (-not $env:POSTGRES_TEST_CONNECTION) {
                Write-Host 'NOTE: POSTGRES_TEST_CONNECTION is not set. Integration tests will be SKIPPED' -ForegroundColor Yellow
                Write-Host '      unless a container runtime is available. They are NOT passing — they are absent.' -ForegroundColor Yellow
            }

            $filterArgs = @()
            if ($IntegrationFilter) {
                # TASK-0067: this dotnet test call is the SAME one the unfiltered run uses, inside
                # THIS script, after Initialize-PostgresTestConnection already ran at the top -- so
                # POSTGRES_TEST_CONNECTION resolves exactly as it does for a full run. A raw
                # `dotnet test --filter` run outside ci.ps1 does not resolve it at all; that gap is
                # what let TASK-0063 report `Passed: 0, Skipped: 1` and exit 0 on a test that never
                # really ran.
                Write-Host "Filtering integration tests: $IntegrationFilter" -ForegroundColor DarkGray
                $filterArgs = @('--filter', $IntegrationFilter)
            }

            # Snapshotted BEFORE the run, by full path, not by "newest timestamp": the Unit &
            # architecture tests gate above always wipes and repopulates './artifacts/coverage', so
            # by the time this gate runs the directory already contains that suite's own .trx. A
            # "pick whichever .trx has the latest LastWriteTime" check would silently select THAT
            # file whenever this run's filter produces none of its own, read its total (the whole
            # unit suite, never zero), and let a filter that matched nothing read as a pass -- a
            # review caught exactly this hole. Comparing file identity (which paths are NEW after
            # this run) rather than timestamps sidesteps clock/resolution questions entirely.
            $existingTrxPaths = @(
                Get-ChildItem './artifacts/coverage' -Filter '*.trx' -ErrorAction SilentlyContinue |
                    Select-Object -ExpandProperty FullName
            )

            dotnet test 'tests/SchoolManagement.IntegrationTests/SchoolManagement.IntegrationTests.csproj' --no-build --configuration $Configuration --nologo `
                --settings coverlet.runsettings `
                --results-directory './artifacts/coverage' `
                --logger 'trx' `
                @filterArgs
            $testExitCode = $LASTEXITCODE

            if ($IntegrationFilter) {
                # TASK-0067: a filter matching ZERO tests is a FAILURE, not a clean pass -- the same
                # false-green class as the silent skip this card exists to kill. `dotnet test` exits
                # 0 when a filter selects nothing, so the exit code alone cannot catch this; the trx
                # this run just wrote (a path that was NOT in the snapshot above) is read directly,
                # not via the merged coverage-gate summary, which only checks the aggregate and would
                # not single out an empty suite from the unit suite's already-present total.
                $newTrx = @(Get-ChildItem './artifacts/coverage' -Filter '*.trx' -ErrorAction SilentlyContinue) |
                    Where-Object { $existingTrxPaths -notcontains $_.FullName } |
                    Sort-Object LastWriteTime -Descending | Select-Object -First 1

                if (-not $newTrx) {
                    throw "Integration test filter '$IntegrationFilter' produced no .trx result file for this run -- it matched zero tests."
                }

                [xml]$filterResults = Get-Content -LiteralPath $newTrx.FullName -Raw
                $filterTotal = [int]$filterResults.TestRun.ResultSummary.Counters.total
                if ($filterTotal -eq 0) {
                    throw "Integration test filter '$IntegrationFilter' matched ZERO tests. Check the expression -- a typo'd filter reporting green is a false pass."
                }
            }

            $global:LASTEXITCODE = $testExitCode
        }
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

        # TASK-0067: a real failure, or a test reporting itself skipped (e.g. Postgres unreachable),
        # still fails the run in EVERY mode -- -SkipIntegration/-IntegrationFilter only excuses the
        # FLOOR below from being enforced against a partial number, never this check. This is the
        # exact distinction the card exists to keep safe: a stage skipped by request is not the same
        # as a test that ran and skipped itself.
        if (-not $verdict.Passed) {
            throw 'Test run was not clean (failed and/or skipped tests) - see the messages above.'
        }

        if ($script:integrationScoped) {
            # TASK-0067: the integration stage was skipped or filtered by request, so this run is
            # necessarily partial and the coverage number is not comparable to the floor -- it is not
            # a pass (nothing was proven against the floor) and not a failure (nothing said it had to
            # be). $script:coverageGateNotApplicable is read right after this Invoke-Gate call below
            # to correct the single "PASS: Coverage threshold" line Invoke-Gate is about to record,
            # to N/A, in the SUMMARY block.
            Write-Host ''
            Write-Host 'NOT APPLICABLE: integration tests were scoped by request (-SkipIntegration or' -ForegroundColor Yellow
            Write-Host '-IntegrationFilter), so this run is partial and the coverage floor is not enforced' -ForegroundColor Yellow
            Write-Host 'against it. This is reported as N/A, not PASS or FAIL, in the final summary.' -ForegroundColor Yellow
            $script:coverageGateNotApplicable = $true
            $global:LASTEXITCODE = 0
            return
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

    if ($script:coverageGateNotApplicable) {
        # TASK-0067: Invoke-Gate above just recorded "PASS: Coverage threshold (...)" (exit code 0,
        # no exception) and printed it to the console -- correct only the SUMMARY-block entry to N/A
        # so the pasted evidence cannot be misread as a genuine pass on a partial run's coverage
        # number. $failures is untouched: this is not a failure, so the run's exit code is unaffected.
        $lastIndex = $gateVerdicts.Count - 1
        $gateVerdicts[$lastIndex] = "N/A: Coverage threshold ($CoverageThreshold%) -- integration scoped by request (-SkipIntegration or -IntegrationFilter); floor not enforced against a partial run"
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

        # TWO passes, deliberately (TASK-0032). `gitleaks detect` alone scans committed HISTORY
        # only, so a secret introduced by the dispatch under test — and never committed — is
        # invisible to it: the gate went green for the author who introduced one and red for
        # whoever ran next (TASK-0027 closed clean, TASK-0028 dispatch 1 found it red having
        # touched none of the flagged files). The first pass is unchanged and still catches
        # anything already in history; the second treats the source as a plain directory instead
        # of a git repo (`--no-git`), so it sees exactly the files on disk right now, staged or
        # not, committed or not. Proven by backend/scripts/tests/secret-scan-working-tree.tests.ps1,
        # which plants a fake secret in a never-committed file and shows the first form misses it
        # and the second catches it.
        Write-Host 'Pass 1 of 2: committed history...'
        gitleaks detect --source . --config .gitleaks.toml --redact --no-banner
        $historyExitCode = $LASTEXITCODE

        # Whole-repo, not just backend/: pass 1 already covers the whole repo despite the
        # Push-Location above, because git history discovery walks up to the repository root
        # regardless of cwd. Pass 2 has no git repo to discover from once --no-git is set, so it is
        # pointed at the repo root explicitly — otherwise it would silently narrow coverage to
        # backend/** alone and miss a secret landing in contracts/** or frontend/**.
        $repoRoot = Split-Path -Parent $backendRoot

        Write-Host ''
        Write-Host 'Pass 2 of 2: working tree (uncommitted changes included)...'
        gitleaks detect --source $repoRoot --no-git --config .gitleaks.toml --redact --no-banner
        $workingTreeExitCode = $LASTEXITCODE

        if ($historyExitCode -ne 0 -or $workingTreeExitCode -ne 0) {
            $global:LASTEXITCODE = 1
        }
        else {
            $global:LASTEXITCODE = 0
        }
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
