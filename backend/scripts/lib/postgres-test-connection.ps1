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

    TASK-0078 (2026-09-17, human directive): the file is no longer read by default. A gate run
    uses the LOCAL container (Testcontainers, resolved independently by
    SchoolManagement.IntegrationTests/Infrastructure/DatabaseAvailability.cs at test-host start)
    unless the human has explicitly confirmed the hosted database for THIS run via -UseHostedDb.
    The file does NOT "win" over the container by default any more — that was exactly the bug
    (2026-09-17: a silent hosted-Neon run turned a ~4-minute local stage into 45+ minutes while the
    ledger claimed container-backed runs).

    Resolution order, first match wins:

      1. $env:POSTGRES_TEST_CONNECTION, if already set in this process (whatever exported it —
         a shell profile, a parent shell, CI's own service-container wiring — is trusted as-is) —
         BUT, outside CI (-IsCi not passed), a value whose host is not localhost/127.0.0.1/::1 is
         REJECTED (Initialize-PostgresTestConnection throws) unless -UseHostedDb is also passed.
         A hosted connection must never arrive by an ambient environment variable either. Inside
         CI (-IsCi), an explicit value always wins outright, no host check — that is CI's own
         service container wiring (backend-ci.yml), which is sanctioned regardless of host.
      2. <HomeDirectory>/.gras/pg-test.txt — read ONLY when -UseHostedDb is passed. Without the
         switch the file is ignored even when present and non-empty, and POSTGRES_TEST_CONNECTION
         is left unset so the local container path (DatabaseAvailability.cs's own resolution)
         engages instead. Once read (with -UseHostedDb), a leading UTF-8 BOM (the real file on
         this machine starts EF BB BF, confirmed by byte inspection) and surrounding whitespace
         are stripped.
      3. Otherwise POSTGRES_TEST_CONNECTION is left unset — the local-container path.

    A missing or empty file (even with -UseHostedDb passed) is NOT fatal and raises no warning of
    its own here: the existing "Integration tests will be SKIPPED / they are NOT passing — they
    are absent" warning inside ci.ps1's Integration tests gate is the ONLY place that message
    lives, and it still fires, unchanged, whenever POSTGRES_TEST_CONNECTION is still unset after
    this file's function runs.

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

function Get-PostgresConnectionHost {
    <#
    .SYNOPSIS
        Pure: extracts the Host (or Server) key from an ADO.NET/Npgsql-style connection string
        ("Key=Value;Key=Value;..."), case-insensitively on both the key and the returned value's
        surrounding whitespace. Returns $null when neither key is present, the value is empty, or
        $ConnectionString itself is null/empty/whitespace. Never throws on a malformed string.
    #>
    [CmdletBinding()]
    param(
        [AllowNull()]
        [string]$ConnectionString
    )

    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        return $null
    }

    foreach ($pair in $ConnectionString -split ';') {
        $kv = $pair -split '=', 2
        if ($kv.Count -eq 2) {
            $key = $kv[0].Trim()
            if ($key -ieq 'Host' -or $key -ieq 'Server') {
                $value = $kv[1].Trim()
                if (-not [string]::IsNullOrEmpty($value)) {
                    return $value
                }
            }
        }
    }

    return $null
}

function Test-IsLocalPostgresHost {
    <#
    .SYNOPSIS
        Pure: true only for localhost, 127.0.0.1, or ::1 (any bracketing, case-insensitive).
        $null/empty/unparsed is NOT local — this fails CLOSED (treated as a hosted host, subject to
        the -UseHostedDb check) rather than silently letting an unparsable value through as local.
    #>
    [CmdletBinding()]
    param(
        [AllowNull()]
        [string]$HostName
    )

    if ([string]::IsNullOrWhiteSpace($HostName)) {
        return $false
    }

    $normalized = $HostName.Trim().Trim('[', ']')
    return $normalized -in @('localhost', '127.0.0.1', '::1')
}

function Initialize-PostgresTestConnection {
    <#
    .SYNOPSIS
        Resolves $env:POSTGRES_TEST_CONNECTION for THIS process per TASK-0078's order (see this
        file's top-level .DESCRIPTION) and returns a [pscustomobject] describing which path was
        taken, so a caller (ci.ps1) can print the "Integration database: ..." line from ONE place
        rather than re-deriving it. Throws a terminating error — never silently falls back — when
        an explicit, non-local POSTGRES_TEST_CONNECTION is found outside CI without -UseHostedDb.

    .OUTPUTS
        [pscustomobject]@{ Source = <one of 'ExplicitCi' | 'ExplicitLocal' | 'ExplicitHosted' |
        'HostedFile' | 'Unset'> }. 'Unset' covers both "nothing configured, container path" and
        "-UseHostedDb passed but the file resolved to nothing" — in both cases
        POSTGRES_TEST_CONNECTION stays unset and the local-container path is what actually runs.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$HomeDirectory,
        [switch]$UseHostedDb,
        [switch]$IsCi
    )

    if ($env:POSTGRES_TEST_CONNECTION) {
        if ($IsCi) {
            # CI's own service container wiring (backend-ci.yml) — sanctioned outright, no host
            # check. Left alone, per the resolution order above.
            return [pscustomobject]@{ Source = 'ExplicitCi' }
        }

        $hostName = Get-PostgresConnectionHost -ConnectionString $env:POSTGRES_TEST_CONNECTION
        if (Test-IsLocalPostgresHost -HostName $hostName) {
            return [pscustomobject]@{ Source = 'ExplicitLocal' }
        }

        if (-not $UseHostedDb) {
            # Never print the connection string itself, on any path -- only the host token, which
            # is not a credential.
            throw ("POSTGRES_TEST_CONNECTION is set to a non-local host ('$hostName') outside CI, " +
                "without -UseHostedDb. A hosted database must never be reached by an ambient " +
                "environment variable. Either unset it to use the local container, or re-run with " +
                "-UseHostedDb only after the human has confirmed the hosted database for this run.")
        }

        return [pscustomobject]@{ Source = 'ExplicitHosted' }
    }

    if (-not $UseHostedDb) {
        # TASK-0078: the file is no longer consulted at all without the switch -- left unset on
        # purpose so the local-container path engages. The Integration tests gate's own "will be
        # SKIPPED" warning is what the user sees if that path also comes up empty; inventing a
        # second message here would be a second failure mode for the same absence.
        return [pscustomobject]@{ Source = 'Unset' }
    }

    $file = Join-Path $HomeDirectory '.gras/pg-test.txt'
    $resolved = Get-PostgresTestConnectionFromFile -HomeDirectory $HomeDirectory
    if ($resolved) {
        $env:POSTGRES_TEST_CONNECTION = $resolved
        Write-Host "POSTGRES_TEST_CONNECTION resolved from: file at $file" -ForegroundColor DarkGray
        return [pscustomobject]@{ Source = 'HostedFile' }
    }

    # -UseHostedDb was passed but the file is missing/empty -- nothing was actually confirmed to
    # exist, so this falls through to the same 'Unset' (container) path as the default case.
    return [pscustomobject]@{ Source = 'Unset' }
}

function Get-IntegrationFailureGuidance {
    <#
    .SYNOPSIS
        Pure: the guidance ci.ps1 prints when the Integration tests gate FAILS, or $null when the
        source carries no such guidance. Orchestrator ruling, TASK-0078 (2026-09-17), replacing the
        card's original pre-flight-probe AC: the safety property already holds without a probe --
        DatabaseAvailability.cs THROWS (never skips) when no Docker endpoint answers, per its own
        TASK-0065 contract. This function only supplies the extra steer for a human reading the
        failure: for the local-container source ('Unset'), do not silently reach for the hosted
        database. Every other source already went through a database a human explicitly chose
        (CI's own wiring, an explicit local/hosted connection, or a confirmed hosted file), so
        there is nothing to steer them away from -- they return $null.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('ExplicitCi', 'ExplicitLocal', 'ExplicitHosted', 'HostedFile', 'Unset')]
        [string]$Source
    )

    if ($Source -ne 'Unset') {
        return $null
    }

    return "If the local container failed to start, do NOT fall back to the hosted database " +
        "without the human's confirmation. Ask them to check the WSL Docker daemon; re-run with " +
        "-UseHostedDb only after they confirm."
}
