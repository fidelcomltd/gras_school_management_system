<#
.SYNOPSIS
    Resolves POSTGRES_TEST_CONNECTION so a dispatch never has to hand-build a compound command to
    set it before running a gate script (TASK-0031).

.DESCRIPTION
    Dot-source this file (`. lib/postgres-test-connection.ps1`) to pull in
    Get-PostgresTestConnectionFromFile and Initialize-PostgresTestConnection — this is how ci.ps1
    uses it, and it is also how backend/scripts/tests/postgres-test-connection.tests.ps1 exercises
    the SAME functions directly, so there is exactly one implementation of "how the string is
    found", not a second copy in the test hand-verified by eye.

    Resolution order, first match wins — an explicitly set environment variable ALWAYS wins over
    the file, unconditionally:

      1. $env:POSTGRES_TEST_CONNECTION, if already set in this process (whatever exported it —
         a shell profile, a parent shell, CI's own service-container wiring — is trusted as-is).
      2. <HomeDirectory>/.gras/pg-test.txt, if present and non-empty once a leading UTF-8 BOM (the
         real file on this machine starts EF BB BF, confirmed by byte inspection) and surrounding
         whitespace are stripped.

    A missing or empty file is NOT fatal and raises no warning of its own here: the existing
    "Integration tests will be SKIPPED / they are NOT passing — they are absent" warning inside
    ci.ps1's Integration tests gate is the ONLY place that message lives, and it still fires,
    unchanged, whenever POSTGRES_TEST_CONNECTION is still unset after this file's function runs.

    NEVER prints the resolved connection string, on any path, at any verbosity. The only
    diagnostic (a plain Write-Host naming which FILE PATH resolved it, never the file's contents)
    fires exactly once, only on the auto-resolved-from-file path — proven, not merely intended:
    backend/scripts/tests/postgres-test-connection.tests.ps1 plants a distinctive marker string in
    a fixture file and asserts it never appears in any captured output stream, including under
    -Verbose.

    This file deliberately declares NO top-level param() block, for the same reason
    lib/gate-summary.ps1 does not: ci.ps1 dot-sources both files, and PowerShell re-binds a
    script's param() block on every dot-source, which would silently reset an identically-named
    variable already in ci.ps1's own scope back to this file's default. Neither function below
    happens to collide with one of ci.ps1's parameter names today, but the discipline is kept
    anyway so the next addition to this file does not have to re-derive why it matters.
#>

Set-StrictMode -Version Latest

function Get-PostgresTestConnectionFromFile {
    <#
    .SYNOPSIS
        Pure: reads <HomeDirectory>/.gras/pg-test.txt, strips a leading UTF-8 BOM and surrounding
        whitespace, and returns the result — or $null if the file is absent, unreadable, or empty
        after trimming. Never writes to $env:, never prints anything, on any path.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$HomeDirectory
    )

    $file = Join-Path $HomeDirectory '.gras/pg-test.txt'
    if (-not (Test-Path -LiteralPath $file)) {
        return $null
    }

    $raw = Get-Content -LiteralPath $file -Raw -ErrorAction SilentlyContinue
    if ([string]::IsNullOrEmpty($raw)) {
        return $null
    }

    # TrimStart([char]0xFEFF) strips the BOM CHARACTER regardless of whether the particular
    # Get-Content encoding path already stripped the BOM BYTES on read (Windows PowerShell 5.1's
    # default auto-detection already does; that is not a guarantee this function can lean on for
    # every host it might run on, so this is not redundant — it is the part of the contract that
    # does not depend on which Get-Content behaviour happens to be present).
    $trimmed = $raw.TrimStart([char]0xFEFF).Trim()
    if ([string]::IsNullOrEmpty($trimmed)) {
        return $null
    }

    return $trimmed
}

function Initialize-PostgresTestConnection {
    <#
    .SYNOPSIS
        Sets $env:POSTGRES_TEST_CONNECTION for THIS process, if and only if it is not already set,
        by reading <HomeDirectory>/.gras/pg-test.txt. Idempotent. The one function ci.ps1 calls.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$HomeDirectory
    )

    if ($env:POSTGRES_TEST_CONNECTION) {
        # Already resolved by whatever set it — left alone, per the resolution order above. No
        # message here: this is the ordinary, already-documented path every existing gate run and
        # every CI run takes, not something new this file needs to narrate.
        return
    }

    $file = Join-Path $HomeDirectory '.gras/pg-test.txt'
    $resolved = Get-PostgresTestConnectionFromFile -HomeDirectory $HomeDirectory
    if ($resolved) {
        $env:POSTGRES_TEST_CONNECTION = $resolved
        Write-Host "POSTGRES_TEST_CONNECTION resolved from: file at $file" -ForegroundColor DarkGray
    }
    # else: left unset on purpose. The Integration tests gate's own "will be SKIPPED" warning is
    # what the user sees; inventing a second message here would be a second failure mode for the
    # same absence, which the card explicitly rules out.
}
