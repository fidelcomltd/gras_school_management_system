<#
.SYNOPSIS
    Shared VSTest .trx parsing and pass/fail/skip verdict logic for ci.ps1's test gates.

.DESCRIPTION
    Dot-source this file (`. lib/gate-summary.ps1`) to pull in Get-TrxSummary and
    Get-TestRunVerdict for use inside another script — this is how ci.ps1 uses it, so there is
    exactly one implementation of "what does this batch of .trx files mean", not one in ci.ps1 and
    a second, subtly different one duplicated for tests.

    Invoked DIRECTLY as a script instead (`./gate-summary.ps1 -ResultsDirectory <dir>`), it reads
    every *.trx in that directory, prints the same verdict, and exits 0 (pass) or 1 (fail). This is
    what backend/scripts/tests/gate-summary.tests.ps1 drives in a child process, to assert on the
    real process exit code — dot-sourcing a fixture into the calling session has no exit code to
    observe.

    Script-mode arguments (NOT a formal param() block — see why below):

      -ResultsDirectory <dir>   Directory containing one or more *.trx files, as produced by
                                `dotnet test --logger trx`.
      -AllowSkipped             Do not fail the verdict when tests were skipped / not executed.
                                The skip is still reported by name; only whether it flips the exit
                                code changes.

    This file deliberately declares NO top-level param() block. PowerShell binds a script's
    param() block on EVERY dot-source, including a bare `. gate-summary.ps1` with no arguments at
    all — which would silently reset any identically-named variable already in the caller's scope
    to this file's own default. ci.ps1 dot-sources this file AND has its own -AllowSkipped switch;
    a param() here with that same name would silently clobber ci.ps1's real -AllowSkipped value
    back to $false on every run (caught live: `ci.ps1 -AllowSkipped` still failed the Coverage
    threshold gate on a skipped run, because dot-sourcing this file overwrote it). Parsing $args by
    hand instead means dot-sourcing this file can never touch a variable in the caller's scope.
#>

Set-StrictMode -Version Latest

function Get-SuiteName {
    <#
    .SYNOPSIS
        Derives which test project a .trx came from, e.g. "schoolmanagement.integrationtests".

    .DESCRIPTION
        A VSTest trx FILENAME carries only a user, a machine name and a timestamp (observed on this
        machine: HP_DESKTOP-GPLBVV9_2026-09-04_14_12_56.trx) — it never names the project, so
        printing it in a skip message tells nobody which of several test projects skipped. The
        assembly path does: TestRun.TestDefinitions.UnitTest[0].storage is the full path to the
        test DLL (VSTest lower-cases it), and its leaf filename without extension names the
        project — e.g. ...\tests\schoolmanagement.integrationtests\bin\release\net10.0\
        schoolmanagement.integrationtests.dll -> "schoolmanagement.integrationtests".

        Falls back to $FallbackName (the trx filename) when TestDefinitions is absent, empty, or
        storage is missing/blank — an empty or malformed trx degrades to the least useful but still
        safe answer rather than throwing. Every step is guarded with try/catch because
        Set-StrictMode -Version Latest THROWS on a missing XML child element or attribute; it does
        not return $null the way untyped PowerShell usually would.
    #>
    param(
        [Parameter(Mandatory)][xml]$Results,
        [Parameter(Mandatory)][string]$FallbackName
    )

    $testDefinitions = $null
    try { $testDefinitions = $Results.TestRun.TestDefinitions } catch { $testDefinitions = $null }
    if (-not $testDefinitions) {
        return $FallbackName
    }

    $unitTests = $null
    try { $unitTests = $testDefinitions.UnitTest } catch { $unitTests = $null }
    if (-not $unitTests) {
        return $FallbackName
    }

    $storage = $null
    try { $storage = (@($unitTests))[0].storage } catch { $storage = $null }
    if ([string]::IsNullOrWhiteSpace($storage)) {
        return $FallbackName
    }

    # Split on '/' AND '\' explicitly rather than delegating to [System.IO.Path]. That type's
    # separator set is OS-conditional: Windows recognizes both '/' and '\', but Unix recognizes
    # ONLY '/' — a backslash there is just an ordinary filename character. `storage` is written by
    # whichever OS ran the tests, so a .trx produced on a Windows dev box (backslash paths) can be
    # parsed on Linux CI, and vice versa. Delegating to System.IO.Path would silently return the
    # entire mangled path instead of the leaf project name on whichever host's separator convention
    # didn't match the .trx's origin.
    $leaf = (@($storage -split '[\\/]'))[-1]
    if ([string]::IsNullOrWhiteSpace($leaf)) {
        return $FallbackName
    }

    # Strip only the LAST extension (mirrors GetFileNameWithoutExtension), not every dot — a suite
    # name itself may contain no dots, but do not assume that; only ever remove one trailing
    # ".ext" segment.
    $suite = $leaf -replace '\.[^.\\/]+$', ''
    if ([string]::IsNullOrWhiteSpace($suite)) {
        return $FallbackName
    }

    return $suite
}

function Get-TrxSummary {
    <#
    .SYNOPSIS
        Reads every *.trx file in a directory and returns per-file and total test counters.

    .DESCRIPTION
        `Skipped` is DERIVED as total - passed - failed, NOT read from the trx `notExecuted`
        counter. xunit v3's dynamic skips (Assert.Skip) do not increment `notExecuted` in the
        VSTest trx — a fully-skipped project reports total=31, passed=0, failed=0, notExecuted=0 —
        so trusting that counter would silently conclude an empty run was complete.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$ResultsDirectory
    )

    $files = [System.Collections.Generic.List[pscustomobject]]::new()

    # NOT -Recurse: the VSTest logger writes each project's .trx directly into $ResultsDirectory,
    # one level only (matches the original hand-rolled loop this replaces). The test host also
    # creates a <trx-name>/In/<machine>/ subdirectory per run, but only to hold a COPY of
    # coverage.cobertura.xml as an attachment — never a copy of the .trx itself (verified against a
    # real three-project run: three top-level .trx files, zero .trx anywhere under an `In/`
    # subdirectory). Recursing here would risk double-counting if that ever changed; the
    # ReportGenerator glob a few gates down deliberately avoids the same trap for the same reason.
    $trxFiles = @(Get-ChildItem -LiteralPath $ResultsDirectory -Filter '*.trx' -ErrorAction SilentlyContinue)

    foreach ($trx in $trxFiles) {
        # -LiteralPath, not positional: a VSTest-disambiguated filename can carry a literal "[1]"
        # suffix (...HH_mm_ss[1].trx), which PowerShell's provider otherwise resolves the
        # unbracketed square brackets as a wildcard character class and fails to find the file.
        [xml]$results = Get-Content -LiteralPath $trx.FullName -Raw
        $counters = $results.TestRun.ResultSummary.Counters

        $total = [int]$counters.total
        $passed = [int]$counters.passed
        $failed = [int]$counters.failed
        $skipped = $total - $passed - $failed
        $suite = Get-SuiteName -Results $results -FallbackName $trx.Name

        $files.Add([pscustomobject]@{
            Name    = $trx.Name
            Suite   = $suite
            Total   = $total
            Passed  = $passed
            Failed  = $failed
            Skipped = $skipped
        })
    }

    $totals = [pscustomobject]@{
        Total   = [int](($files | Measure-Object -Property Total -Sum).Sum)
        Passed  = [int](($files | Measure-Object -Property Passed -Sum).Sum)
        Failed  = [int](($files | Measure-Object -Property Failed -Sum).Sum)
        Skipped = [int](($files | Measure-Object -Property Skipped -Sum).Sum)
    }

    [pscustomobject]@{
        Files  = $files
        Totals = $totals
    }
}

function Get-TestRunVerdict {
    <#
    .SYNOPSIS
        Turns a Get-TrxSummary result into a pass/fail verdict plus the messages ci.ps1 prints.

    .DESCRIPTION
        Fails when no trx files were found at all (the suite never ran), when any test FAILED, or
        when any test was skipped/not-executed and -AllowSkipped was not passed. A skip is reported
        by the SUITE (the test project, from Get-SuiteName) rather than the trx filename — a trx
        filename is only a machine, a user and a timestamp, which does not tell anyone which of
        several test projects skipped — so an agent knows which project to go look at (CLAUDE.md
        §13: a skipped suite is not a passing suite).
    #>
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Summary,

        [switch]$AllowSkipped
    )

    $messages = [System.Collections.Generic.List[string]]::new()

    if ($Summary.Files.Count -eq 0) {
        $messages.Add('No .trx result files were found - the test gate above this one did not run or produced no results.')
        return [pscustomobject]@{ Passed = $false; Messages = $messages }
    }

    $passed = $true

    if ($Summary.Totals.Failed -gt 0) {
        $passed = $false
        $messages.Add("$($Summary.Totals.Failed) test(s) FAILED.")
    }

    if ($Summary.Totals.Skipped -gt 0) {
        foreach ($file in $Summary.Files) {
            if ($file.Skipped -gt 0) {
                $messages.Add("SKIPPED: $($file.Suite) - $($file.Skipped) of $($file.Total) test(s) not executed (passed=$($file.Passed) failed=$($file.Failed)).")
            }
        }

        if (-not $AllowSkipped) {
            $passed = $false
        }
    }

    [pscustomobject]@{
        Passed   = $passed
        Messages = $messages
    }
}

# ── Script mode ──────────────────────────────────────────────────────────────────────────────
# Only when invoked directly (not dot-sourced): read the directory, print the verdict, exit 0/1.
# PowerShell reports $MyInvocation.InvocationName as literally '.' for a dot-sourced call; any
# other value (a path, or '&') means this file is running as its own script.
if ($MyInvocation.InvocationName -ne '.') {
    # Hand-parsed, not a param() block — see the file header for why. $args holds every token
    # PowerShell did not otherwise bind (there is nothing else here to bind them to).
    $scriptResultsDirectory = $null
    $scriptAllowSkipped = $false

    for ($i = 0; $i -lt $args.Count; $i++) {
        switch ($args[$i]) {
            '-ResultsDirectory' {
                $i++
                if ($i -lt $args.Count) {
                    $scriptResultsDirectory = $args[$i]
                }
            }
            '-AllowSkipped' {
                $scriptAllowSkipped = $true
            }
        }
    }

    if (-not $scriptResultsDirectory) {
        Write-Host 'Usage: gate-summary.ps1 -ResultsDirectory <dir> [-AllowSkipped]' -ForegroundColor Red
        exit 1
    }

    $summary = Get-TrxSummary -ResultsDirectory $scriptResultsDirectory
    $verdict = Get-TestRunVerdict -Summary $summary -AllowSkipped:$scriptAllowSkipped

    foreach ($line in $verdict.Messages) {
        Write-Host $line
    }

    Write-Host ("total={0} passed={1} failed={2} skipped={3}" -f $summary.Totals.Total, $summary.Totals.Passed, $summary.Totals.Failed, $summary.Totals.Skipped)

    if ($verdict.Passed) {
        Write-Host 'PASS' -ForegroundColor Green
        exit 0
    }

    Write-Host 'FAIL' -ForegroundColor Red
    exit 1
}
