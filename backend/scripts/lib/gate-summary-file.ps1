<#
.SYNOPSIS
    Writes the gate run's SUMMARY to backend/artifacts/gate-summary.txt (TASK-0056).

.DESCRIPTION
    So an agent reporting a gate quotes one small file instead of scanning the log. Written on EVERY exit path of ci.ps1:
    a pass, a failure, a fail-fast abort and an early abort before any gate ran. The shape:

        PASS | FAIL                         (first line, one token)
        ABORTED: <reason>                   (only when the run stopped before its summary)
        PASS: <gate> / FAIL: <gate> -- ...  (each gate that ran, as the console SUMMARY prints it)
        SKIPPED-NOT-RUN: <gate>             (each gate a fail-fast abort never reached)
        Tests: total=.. passed=.. ...       (when tests ran)
        Coverage: line=.. branch=..         (when coverage was measured)
        FAILING LINES:                      (the failing gates' error lines, in full)

    Plain text, UTF-8 without a byte-order mark, so the first line is exactly the token.
#>

# ci.ps1's gates in run order. A gate absent from the run's verdicts is listed as SKIPPED-NOT-RUN.
$script:GateSummaryAllGates = @(
    'Restore',
    'Format',
    'Build (warnings as errors)',
    'Generate OpenAPI document (no database)',
    'Unit & architecture tests',
    'Integration tests',
    'Coverage threshold',
    'Vulnerable dependencies',
    'Secret scan',
    'OpenAPI contract drift',
    'Contract ledger drift'
)

function Get-GateFailingLines {
    <#
    .SYNOPSIS
        The error lines in a gate's captured output: compiler and analyzer errors (`: error ...`), failed tests
        (`Failed <name>`), `FAIL`/`ERROR` lines, a test verdict's skipped or failed tests, gitleaks' findings. Each
        once, in order; a tally such as "0 Error(s)" is not one.
    #>
    param([object[]]$Output)

    $seen = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($line in @($Output)) {
        $text = "$line".TrimEnd()
        $isFailing = $text -match '(:\s*error\b)|(^\s*Failed\s)|(\bFAIL\b)|(^\s*ERROR\b)|(leaks found)|(\b(skipped|failed)=[1-9])|(Skipped \d*[1-9])'
        if ($isFailing -and $text -notmatch '\b0 Error\(s\)' -and $seen.Add($text.Trim())) {
            $text.Trim()
        }
    }
}

function Write-GateSummaryFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [string[]]$Verdicts = @(),
        [string]$TestCountsLine,
        [string]$CoverageLine,
        [string[]]$FailingLines = @(),
        [string]$AbortReason
    )

    $verdictLines = @($Verdicts | Where-Object { $_ })
    $failed = [bool]$AbortReason -or [bool]($verdictLines | Where-Object { $_ -like 'FAIL:*' })

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add($(if ($failed) { 'FAIL' } else { 'PASS' }))
    if ($AbortReason) {
        $lines.Add("ABORTED: $AbortReason")
    }

    foreach ($verdict in $verdictLines) {
        $lines.Add($verdict)
    }

    foreach ($gate in $script:GateSummaryAllGates) {
        $ran = $verdictLines | Where-Object { $_ -like "*: $gate*" }
        if (-not $ran) {
            $lines.Add("SKIPPED-NOT-RUN: $gate")
        }
    }

    if ($TestCountsLine) { $lines.Add($TestCountsLine) }
    if ($CoverageLine) { $lines.Add($CoverageLine) }

    $failing = @($FailingLines | Where-Object { $_ })
    if ($failing.Count -gt 0) {
        $lines.Add('FAILING LINES:')
        foreach ($line in $failing) {
            $lines.Add("  $line")
        }
    }

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    [System.IO.File]::WriteAllLines($Path, $lines, [System.Text.UTF8Encoding]::new($false))
}
