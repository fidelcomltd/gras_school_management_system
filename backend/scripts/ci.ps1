<#
.SYNOPSIS
    Runs every quality gate. The SAME script CI runs, so "it passes on my machine" means something.

.DESCRIPTION
    Gates, in order (cheapest and most likely to fail first, so feedback is fast):

      1. restore
      2. build with warnings as errors
      3. dotnet format --verify-no-changes
      4. generate the OpenAPI document (no database — SchoolManagement.ArchitectureTests reads it)
      5. tests with coverage collection
      6. coverage threshold
      7. dependency vulnerability scan
      8. secret scan (skipped with a warning if gitleaks is not installed)
      9. OpenAPI contract drift

    Every gate runs even after one fails, then the script exits non-zero with a summary. Stopping at
    the first failure means a contributor fixes one thing, pushes, and waits to discover the next.

.PARAMETER Configuration
    Build configuration. Defaults to Release, matching CI.

.PARAMETER CoverageThreshold
    Minimum line coverage percentage. A FLOOR, NOT A GOAL — see the note where it is checked.

.PARAMETER SkipContractDrift
    Skip the OpenAPI drift check. For the rare case where the contract is being changed deliberately
    in the same commit and has not been promoted yet.

.EXAMPLE
    ./scripts/ci.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateRange(0, 100)]
    [int]$CoverageThreshold = 60,

    [switch]$SkipContractDrift
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

$backendRoot = Split-Path -Parent $PSScriptRoot
Push-Location $backendRoot

$failures = [System.Collections.Generic.List[string]]::new()

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
    }
    catch {
        Write-Host "FAIL: $Name -- $($_.Exception.Message)" -ForegroundColor Red
        $failures.Add($Name)
    }
}

try {
    Invoke-Gate 'Restore' { dotnet restore --nologo }

    Invoke-Gate 'Build (warnings as errors)' {
        # TreatWarningsAsErrors is already set in Directory.Build.props; passing it again makes the
        # intent explicit at the call site and survives someone loosening the props file.
        dotnet build --no-restore --configuration $Configuration --nologo -warnaserror
    }

    Invoke-Gate 'Format' {
        dotnet format --verify-no-changes --no-restore
    }

    Invoke-Gate 'Generate OpenAPI document (no database)' {
        # SchoolManagement.ArchitectureTests reads this artefact directly instead of starting the
        # application (TASK-0009 moved the document-property contract tests off the database, since
        # none of them ever touched it), so it must exist before the Tests gate runs below. This is the
        # ONLY call to generate-openapi.ps1 in this script: the "OpenAPI contract drift" gate further
        # down reuses the same file rather than regenerating it, so there is exactly one path to this
        # document, not two.
        & (Join-Path $PSScriptRoot 'generate-openapi.ps1') -Configuration $Configuration | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw 'Document generation failed.'
        }
        $global:LASTEXITCODE = 0
    }

    Invoke-Gate 'Tests' {
        # Integration tests SKIP when no PostgreSQL is reachable — they never silently pass. To run them
        # here, set POSTGRES_TEST_CONNECTION or make a container runtime available. CI must do one of
        # those, or the suite it is guarding is much smaller than it looks.
        if (-not $env:POSTGRES_TEST_CONNECTION) {
            Write-Host 'NOTE: POSTGRES_TEST_CONNECTION is not set. Integration tests will be SKIPPED' -ForegroundColor Yellow
            Write-Host '      unless a container runtime is available. They are NOT passing — they are absent.' -ForegroundColor Yellow
        }

        # Cleared first: stale reports from a previous run would otherwise be merged into this one and
        # report coverage for code that no longer exists.
        Remove-Item -Recurse -Force './artifacts/coverage' -ErrorAction SilentlyContinue

        # No fixed trx filename. Each of the three test projects writes its own results file, so a fixed
        # name makes them overwrite each other — leaving one arbitrary project's results and a skipped
        # count that silently understates reality.
        dotnet test --no-build --configuration $Configuration --nologo `
            --settings coverlet.runsettings `
            --results-directory './artifacts/coverage' `
            --logger 'trx'
    }

    Invoke-Gate "Coverage threshold ($CoverageThreshold%)" {
        $reports = @(Get-ChildItem './artifacts/coverage' -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue)

        if ($reports.Count -eq 0) {
            throw 'No coverage report was produced. Did the test gate fail?'
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

        # ── Was the suite actually complete? ──────────────────────────────────────────────────────
        # This matters more than the number. When no PostgreSQL is available the integration tests
        # SKIP, so the entire HTTP layer — endpoints, middleware, the pipeline end to end, OpenAPI
        # transformers — is never executed and coverage is structurally low. Enforcing the floor then
        # would fail for a reason unrelated to code quality, and the predictable response would be to
        # lower the floor until it passed, which destroys the gate for everyone.
        #
        # So: enforce the floor only when the suite was complete, and when it was not, report the
        # number and say plainly that it was NOT enforced. Never silently pass a floor that was not
        # checked.
        # Summed across EVERY trx: one file per test project, and it is the integration project's skips
        # that matter here. Reading a single file would miss them entirely.
        #
        # DERIVED as total - passed - failed, NOT read from the `notExecuted` counter. xunit v3's dynamic
        # skips (Assert.Skip) do not increment notExecuted in the VSTest trx: the integration project
        # reports total=31, passed=0, failed=0, notExecuted=0. Trusting notExecuted would silently
        # conclude the suite was complete and enforce a floor against a partial run.
        # -LiteralPath, not positional: when two test projects finish in the same second the VSTest
        # logger disambiguates with a literal "[1]" suffix (...HH_mm_ss[1].trx), and PowerShell's
        # provider path resolution treats unbracketed square brackets as a wildcard character class.
        # Without -LiteralPath that bracketed filename fails to resolve and Get-Content throws a
        # confusing "parameter 'Raw' not found" error instead of a path-not-found one. TASK-0002
        # hit this while adding a fourth trx-producing run; unrelated to the authorisation work itself.
        $skipped = 0
        foreach ($trx in Get-ChildItem './artifacts/coverage' -Filter '*.trx' -ErrorAction SilentlyContinue) {
            [xml]$results = Get-Content -LiteralPath $trx.FullName -Raw
            $counters = $results.TestRun.ResultSummary.Counters

            $notRun = [int]$counters.total - [int]$counters.passed - [int]$counters.failed
            if ($notRun -gt 0) {
                $skipped += $notRun
            }
        }

        if ($skipped -gt 0) {
            Write-Host ''
            Write-Host "COVERAGE FLOOR NOT ENFORCED: $skipped test(s) were skipped, so the suite was" -ForegroundColor Yellow
            Write-Host 'incomplete and this number is not comparable to the floor. The integration tests' -ForegroundColor Yellow
            Write-Host 'need PostgreSQL — set POSTGRES_TEST_CONNECTION or start a container runtime, then' -ForegroundColor Yellow
            Write-Host 'this gate will measure and enforce properly. CI supplies a database, so the floor' -ForegroundColor Yellow
            Write-Host 'IS enforced there (and a separate CI step fails if anything was skipped).' -ForegroundColor Yellow
            $global:LASTEXITCODE = 0
            return
        }

        # THE THRESHOLD IS A FLOOR, NOT A GOAL. It exists to catch a collapse — someone deleting a test
        # project, or a large untested subsystem landing at once. Chasing the number produces tests that
        # execute code without asserting anything about it, which is worse than no test because it looks
        # like cover. Judge a pull request on whether its behaviour is tested, not on whether this moved.
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

    Write-Host ''
    Write-Host '════════════════════════════════════════════════' -ForegroundColor Cyan

    if ($failures.Count -gt 0) {
        Write-Host "FAILED GATES ($($failures.Count)):" -ForegroundColor Red
        $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
        exit 1
    }

    Write-Host 'ALL GATES PASSED' -ForegroundColor Green
    exit 0
}
finally {
    Pop-Location
}
