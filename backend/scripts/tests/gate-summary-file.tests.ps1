<#
.SYNOPSIS
    Self-test for backend/scripts/lib/gate-summary-file.ps1 and ci.ps1's use of it (TASK-0056).

.DESCRIPTION
    Same dependency-free harness as gate-summary.tests.ps1. Proves the summary file appears on a FAILING run, not only a
    passing one: ci.ps1 is run in a child process with two mutually exclusive switches, which aborts before any gate
    (seconds, no build), and the file must say FAIL with every gate SKIPPED-NOT-RUN. The in-process cases cover the PASS
    token, the absence of a byte-order mark, and which captured lines count as failing lines. A real gate failure was
    proven by hand when this was built (a planted compile error: the file named the build error in full).

    Overwrites backend/artifacts/gate-summary.txt, which is only ever the latest run's summary.

    Exits 0 if every assertion passed, 1 otherwise.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptsRoot = Split-Path -Parent $PSScriptRoot
$backendRoot = Split-Path -Parent $scriptsRoot
$summaryPath = Join-Path $backendRoot 'artifacts/gate-summary.txt'
. (Join-Path $scriptsRoot 'lib/gate-summary-file.ps1')

$script:failures = [System.Collections.Generic.List[string]]::new()

function Assert-True {
    param([Parameter(Mandatory)][bool]$Condition, [Parameter(Mandatory)][string]$Message)
    if ($Condition) {
        Write-Host "ok:   $Message" -ForegroundColor DarkGray
    }
    else {
        $script:failures.Add($Message)
        Write-Host "FAIL: $Message" -ForegroundColor Red
    }
}

# 1. A failing run leaves a file that says so.
if (Test-Path $summaryPath) { Remove-Item $summaryPath }
$shell = (Get-Process -Id $PID).Path
& $shell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptsRoot 'ci.ps1') -SkipIntegration -IntegrationFilter 'x' *> $null
$exitCode = $LASTEXITCODE
Assert-True ($exitCode -ne 0) "an aborted run still exits non-zero (was $exitCode)"
Assert-True (Test-Path $summaryPath) 'an aborted run writes gate-summary.txt'
if (Test-Path $summaryPath) {
    $lines = [System.IO.File]::ReadAllLines($summaryPath)
    Assert-True ($lines[0] -ceq 'FAIL') "an aborted run's first line is exactly FAIL (was '$($lines[0])')"
    Assert-True ([bool]($lines -like 'ABORTED: *')) 'an aborted run says why'
    Assert-True (@($lines -like 'SKIPPED-NOT-RUN: *').Count -eq $script:GateSummaryAllGates.Count) 'every gate of an aborted run is SKIPPED-NOT-RUN'
}

# 2. A passing set of verdicts writes PASS, with no byte-order mark before it.
$passPath = Join-Path ([System.IO.Path]::GetTempPath()) "gate-summary-pass-$PID.txt"
Write-GateSummaryFile -Path $passPath -Verdicts @($script:GateSummaryAllGates | ForEach-Object { "PASS: $_" }) `
    -TestCountsLine 'Tests: total=3 passed=3 failed=0 skipped=0' -CoverageLine 'Coverage: line=80.00% branch=70.00%'
$bytes = [System.IO.File]::ReadAllBytes($passPath)
$passLines = [System.IO.File]::ReadAllLines($passPath)
Remove-Item $passPath
Assert-True ($bytes[0] -eq [byte][char]'P') 'the file starts with the verdict itself, no byte-order mark'
Assert-True ($passLines[0] -ceq 'PASS') 'all gates passing gives PASS'
Assert-True (-not ($passLines -like 'SKIPPED-NOT-RUN: *')) 'no gate is SKIPPED-NOT-RUN when all ran'
Assert-True ([bool]($passLines -ceq 'Tests: total=3 passed=3 failed=0 skipped=0')) 'the test totals are included'

# 3. Only error lines are failing lines, each once.
$captured = @(
    'Build started.',
    'C:\x\A.cs(5,35): error CS0029: Cannot implicitly convert type [A.csproj]',
    'C:\x\A.cs(5,35): error CS0029: Cannot implicitly convert type [A.csproj]',
    '    0 Error(s)',
    '  Failed SchoolManagement.Tests.SomeTest [2 s]',
    'C:\x\B.cs(1,1): error IMPORTS: Fix imports ordering.',
    'Build succeeded.'
)
$failing = @(Get-GateFailingLines -Output $captured)
Assert-True ($failing.Count -eq 3) "three distinct failing lines are found (found $($failing.Count))"
Assert-True (-not ($failing -like '*Error(s)*')) "a '0 Error(s)' tally is not a failing line"

# 4. The summary's gate list is ci.ps1's: a gate added or renamed there must be added here, or a fail-fast abort would
#    not list it as SKIPPED-NOT-RUN.
$ciText = Get-Content (Join-Path $scriptsRoot 'ci.ps1') -Raw
$declared = @([regex]::Matches($ciText, "Invoke-Gate\s+['`"]([^'`"(]+)") | ForEach-Object { $_.Groups[1].Value.Trim() })
foreach ($gate in $declared) {
    # Names are read up to a "(" (Coverage threshold carries its figure), so each must start a listed name.
    Assert-True ([bool]($script:GateSummaryAllGates | Where-Object { $_ -like "$gate*" })) "ci.ps1 gate '$gate' is in the summary's gate list"
}
Assert-True ($declared.Count -eq $script:GateSummaryAllGates.Count) "the summary lists exactly ci.ps1's $($declared.Count) gates"

# 5. A failing test verdict and a secret scan finding are failing lines too.
$more = @(Get-GateFailingLines -Output @('Tests: total=5 passed=4 failed=0 skipped=1', 'WRN leaks found: 1', 'Tests: total=5 passed=5 failed=0 skipped=0'))
Assert-True ($more.Count -eq 2) "a skipped-test total and a gitleaks finding are failing lines, a clean total is not (found $($more.Count))"

if ($script:failures.Count -gt 0) {
    Write-Host "$($script:failures.Count) assertion(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host 'All gate-summary-file assertions passed.' -ForegroundColor Green
exit 0
