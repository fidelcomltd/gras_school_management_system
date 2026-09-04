<#
.SYNOPSIS
    Fixture gate script for backend/scripts/tests/local-env.tests.ps1. NOT a real gate — never runs
    dotnet, never touches a database, holds no credential-shaped literal of any kind.

.DESCRIPTION
    Mirrors ci.ps1's actual parameter shape closely enough to prove -GateArgs forwarding works on
    the real argument path rather than on a hand-picked toy: one [ValidateSet] string, one
    non-negative int, three switches, and — the detail that matters most for TASK-0018 — the same
    [CmdletBinding()] attribute. Without it, PowerShell silently drops an unrecognised named
    argument instead of erroring, which would make the "wrong switch name fails loudly" acceptance
    criterion pass for the wrong reason (this script accepting anything) rather than the right one
    (ci.ps1's own parameter binder rejecting it).

    Echoes exactly what it was bound to, one "Name=Value" line per parameter, so the test can assert
    on binding directly instead of scraping PowerShell's own error text. Exits 1 when -Fail is
    passed, so the test can also confirm the exit code survives the two-hop call
    (local-env.template.ps1 -> this script).
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
    [switch]$Fail
)

Write-Output "Configuration=$Configuration"
Write-Output "CoverageThreshold=$CoverageThreshold"
Write-Output "SkipContractDrift=$($SkipContractDrift.IsPresent)"
Write-Output "AllowSkipped=$($AllowSkipped.IsPresent)"
Write-Output "NoFailFast=$($NoFailFast.IsPresent)"

if ($Fail) {
    exit 1
}
exit 0
