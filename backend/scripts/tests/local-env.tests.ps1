<#
.SYNOPSIS
    Dependency-free self-test for backend/scripts/local-env.template.ps1's -GateArgs forwarding
    (TASK-0018). Same hand-rolled harness style as gate-summary.tests.ps1 — this machine has only
    the ancient Windows-bundled Pester 3.4.0 and no `pwsh`.

.DESCRIPTION
    The bug: `[string[]]$GateArgs` splatted with `& $scriptPath @GateArgs` binds POSITIONALLY,
    because splatting an ARRAY passes its elements as ordinary positional values — only splatting a
    HASHTABLE binds by parameter NAME. So `-GateArgs '-NoFailFast'` used to land on ci.ps1's first
    positional parameter (Configuration) instead of being read as a switch. The fix converts the
    token list to an [ordered] hashtable (ConvertTo-SplattableGateArguments, defined inside
    local-env.template.ps1) before splatting.

    This suite exercises the REAL argument path end to end — a genuine child `powershell.exe`
    process running the actual tracked local-env.template.ps1 against a fixture gate script
    (fixtures/fake-gate.ps1), asserting on what the fixture echoes and on the real process exit
    code — not a string assertion against the template's source text.

    Invocation is built as a POWERSHELL SOURCE STRING (quotes and comma-separated array literals)
    executed via `powershell.exe -Command`, not as a raw argv list handed to `-File`. That
    distinction matters here: a raw argv list re-parses each `-Xxx`-shaped token as a NEW named
    parameter the moment it reaches a fresh process's native command-line binder, which breaks
    multi-token forms (two switches, a name/value pair) before local-env.template.ps1 even runs —
    a different, unrelated quirk from the one this task fixes. Typing the same call at an
    interactive PowerShell prompt (what a contributor actually does, and what the acceptance
    criteria describe) parses correctly, and reproducing that fidelity is why `-Command` is used
    here instead of `-File` with a bare argument array.

    fixtures/fake-gate.ps1 mirrors ci.ps1's parameter shape closely enough to prove the fix on the
    real shape (a [ValidateSet] string, a ranged int, three switches) INCLUDING the same
    [CmdletBinding()] attribute ci.ps1 declares — without it, PowerShell silently drops an unknown
    named argument instead of erroring, which would make the "wrong switch name fails loudly"
    assertion below pass for the wrong reason. It never touches a database and holds no
    credential-shaped literal.

    Exits 0 if every assertion passed, 1 otherwise (each failing assertion printed).

.EXAMPLE
    ./scripts/tests/local-env.tests.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$testsRoot = $PSScriptRoot
$scriptsRoot = Split-Path -Parent $testsRoot
$localEnvTemplate = Join-Path $scriptsRoot 'local-env.template.ps1'
$fakeGate = Join-Path (Join-Path $testsRoot 'fixtures') 'fake-gate.ps1'

# -Gate is resolved inside local-env.template.ps1 via `Join-Path $PSScriptRoot $Gate`, which does
# NOT collapse an absolute second path (it naively concatenates) -- so this must be a path relative
# to $scriptsRoot, exactly as a real -Gate value (e.g. 'generate-openapi.ps1') would be.
$relativeFakeGate = Join-Path 'tests' (Join-Path 'fixtures' 'fake-gate.ps1')

if (-not (Test-Path -LiteralPath $localEnvTemplate)) {
    Write-Host "FAIL: cannot find local-env.template.ps1 at '$localEnvTemplate'." -ForegroundColor Red
    exit 1
}
if (-not (Test-Path -LiteralPath $fakeGate)) {
    Write-Host "FAIL: cannot find fixtures/fake-gate.ps1 at '$fakeGate'." -ForegroundColor Red
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

function Invoke-LocalEnvProcess {
    # Runs local-env.template.ps1 in its OWN process via a PowerShell source string (see the long
    # comment above for why -Command rather than -File), targeting the fake-gate.ps1 fixture, and
    # returns the REAL process exit code plus captured stdout/stderr.
    param(
        [string]$GateArgsLiteral = ''  # PowerShell source text for the -GateArgs array, e.g.
                                       # "'-NoFailFast'" or "'-NoFailFast','-AllowSkipped'". Empty
                                       # string omits -GateArgs entirely (the default-path case).
    )

    $command = "& '$localEnvTemplate' -Gate '$relativeFakeGate'"
    if ($GateArgsLiteral) {
        $command += " -GateArgs $GateArgsLiteral"
    }

    # The wrong-switch-name case below makes the CHILD process write a real PowerShell ErrorRecord
    # to its stderr. Under this script's own $ErrorActionPreference = 'Stop', merging that via 2>&1
    # turns it into a terminating NativeCommandError HERE, aborting the whole suite instead of
    # letting the assertions below inspect it -- so run this one call under 'Continue'.
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & powershell.exe -NoProfile -NonInteractive -Command $command 2>&1 | Out-String
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }
    [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output   = $output
    }
}

# ── (a) single switch ────────────────────────────────────────────────────────────────────────────
$single = Invoke-LocalEnvProcess -GateArgsLiteral "'-NoFailFast'"
Assert-True ($single.ExitCode -eq 0) "single switch -NoFailFast: exit code is 0 (got $($single.ExitCode)). Output:`n$($single.Output)"
Assert-True ($single.Output -match 'NoFailFast=True') "single switch -NoFailFast: reaches the gate script as a SWITCH, not a positional Configuration value. Output:`n$($single.Output)"
Assert-True ($single.Output -match 'Configuration=Release') "single switch -NoFailFast: Configuration is untouched (still defaults to Release). Output:`n$($single.Output)"

$singleAllowSkipped = Invoke-LocalEnvProcess -GateArgsLiteral "'-AllowSkipped'"
Assert-True ($singleAllowSkipped.ExitCode -eq 0) "single switch -AllowSkipped: exit code is 0 (got $($singleAllowSkipped.ExitCode)). Output:`n$($singleAllowSkipped.Output)"
Assert-True ($singleAllowSkipped.Output -match 'AllowSkipped=True') "single switch -AllowSkipped: reaches the gate script as a SWITCH. Output:`n$($singleAllowSkipped.Output)"

# ── (b) two switches at once ─────────────────────────────────────────────────────────────────────
$twoSwitches = Invoke-LocalEnvProcess -GateArgsLiteral "'-NoFailFast','-AllowSkipped'"
Assert-True ($twoSwitches.ExitCode -eq 0) "two switches: exit code is 0 (got $($twoSwitches.ExitCode)). Output:`n$($twoSwitches.Output)"
Assert-True ($twoSwitches.Output -match 'NoFailFast=True') "two switches: NoFailFast reaches the gate script. Output:`n$($twoSwitches.Output)"
Assert-True ($twoSwitches.Output -match 'AllowSkipped=True') "two switches: AllowSkipped ALSO reaches the gate script (both, not just the first). Output:`n$($twoSwitches.Output)"

# ── (c) a name/value pair ────────────────────────────────────────────────────────────────────────
$nameValue = Invoke-LocalEnvProcess -GateArgsLiteral "'-Configuration','Debug'"
Assert-True ($nameValue.ExitCode -eq 0) "name/value pair: exit code is 0 (got $($nameValue.ExitCode)). Output:`n$($nameValue.Output)"
Assert-True ($nameValue.Output -match 'Configuration=Debug') "name/value pair: -Configuration Debug reaches the gate script with its VALUE, not as two separate switches. Output:`n$($nameValue.Output)"
Assert-True ($nameValue.Output -match 'NoFailFast=False') "name/value pair: no switch was accidentally set. Output:`n$($nameValue.Output)"

# ── (d) both forms combined in one call ──────────────────────────────────────────────────────────
$combined = Invoke-LocalEnvProcess -GateArgsLiteral "'-NoFailFast','-Configuration','Debug'"
Assert-True ($combined.ExitCode -eq 0) "combined forms: exit code is 0 (got $($combined.ExitCode)). Output:`n$($combined.Output)"
Assert-True ($combined.Output -match 'NoFailFast=True') "combined forms: the switch half still binds. Output:`n$($combined.Output)"
Assert-True ($combined.Output -match 'Configuration=Debug') "combined forms: the name/value half still binds. Output:`n$($combined.Output)"

# ── (e) a wrong switch name still fails loudly, at the GATE SCRIPT's own parameter binder ─────────
# fake-gate.ps1 declares [CmdletBinding()], exactly like ci.ps1, so an unrecognised name is rejected
# by PowerShell's real parameter binder rather than silently accepted -- this wrapper must not
# swallow it into a false success, and must not guess it into some OTHER, misleading error either.
$wrongName = Invoke-LocalEnvProcess -GateArgsLiteral "'-Bogus'"
Assert-True ($wrongName.ExitCode -ne 0) "wrong switch name: exit code is non-zero (got $($wrongName.ExitCode)). Output:`n$($wrongName.Output)"
Assert-True ($wrongName.Output -match 'Bogus') "wrong switch name: the error names the offending parameter. Output:`n$($wrongName.Output)"
Assert-True (-not ($wrongName.Output -match 'Configuration=')) "wrong switch name: the gate script's body never ran (binding failed before invocation, not a silently-accepted no-op). Output:`n$($wrongName.Output)"

# ── (f) the gate script's own exit code still survives the two-hop call ────────────────────────────
$failing = Invoke-LocalEnvProcess -GateArgsLiteral "'-Fail'"
Assert-True ($failing.ExitCode -eq 1) "gate script exit code passthrough: local-env.template.ps1 exits 1 when the gate script does (got $($failing.ExitCode)). Output:`n$($failing.Output)"

# ── (g) regression guard: no -GateArgs at all (today's only working case) still works ──────────────
$noArgs = Invoke-LocalEnvProcess
Assert-True ($noArgs.ExitCode -eq 0) "no -GateArgs: exit code is 0 (got $($noArgs.ExitCode)). Output:`n$($noArgs.Output)"
Assert-True ($noArgs.Output -match 'Configuration=Release') "no -GateArgs: the gate script still runs with its own defaults. Output:`n$($noArgs.Output)"

# ── Never leak the resolved connection string, in any of the runs above ────────────────────────────
# local-env.template.ps1 only ever prints WHICH of its four sources resolved, never the value -- this
# assertion is a tripwire for a regression in that promise, not a claim about what this task changed.
$allOutput = @($single, $singleAllowSkipped, $twoSwitches, $nameValue, $combined, $wrongName, $failing, $noArgs) | ForEach-Object { $_.Output }
Assert-True (-not ($allOutput -match 'Host=.*Port=.*Database=.*Username=.*Password=')) 'no run printed a connection-string-shaped value.'

Write-Host ''
if ($script:failures.Count -gt 0) {
    Write-Host "FAILED: $($script:failures.Count) of $script:assertionCount assertion(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host "PASSED: $script:assertionCount assertion(s)." -ForegroundColor Green
exit 0
