<#
.SYNOPSIS
    Resolves POSTGRES_TEST_CONNECTION for this session, then runs a backend gate script. LOCAL ONLY.

.DESCRIPTION
    ═══════════════════════════════════════════════════════════════════════════════════════
      TEMPLATE — COPY, DO NOT EDIT IN PLACE. Same precedent as
      src/SchoolManagement.Api/appsettings.Development.template.json:

          cp scripts/local-env.template.ps1 scripts/local-env.ps1

      scripts/local-env.ps1 is git-ignored (see backend/.gitignore) and CANNOT be committed by
      accident — it is excluded by an exact-path rule, not a wildcard, so nothing else in
      scripts/ is caught by it. This template stays committed so a new contributor can see
      exactly how the connection string is resolved, and where their own credential goes,
      without a real one ever entering the tree.
    ═══════════════════════════════════════════════════════════════════════════════════════

    WHY AN ENVIRONMENT VARIABLE, NOT dotnet user-secrets:
    tests/SchoolManagement.IntegrationTests/Infrastructure/DatabaseAvailability.cs reads
    POSTGRES_TEST_CONNECTION straight from the process environment, deliberately, so the
    integration-test harness behaves IDENTICALLY locally and in CI (CI supplies the same
    variable from a service container). user-secrets only ever reaches IConfiguration inside
    the Api/Infrastructure host's configuration pipeline — the test harness never builds that
    host's configuration, so a user-secrets value would never arrive here even if you set one.
    This one variable has to be a real environment variable; see backend/README.md
    "Configuration and secrets" for the fuller version of this reasoning.

    RESOLUTION ORDER — the FIRST of these that is set wins. Nothing here ever prints the
    resolved value, on success or on failure — only which of the four sources it came from.

      1. $env:POSTGRES_TEST_CONNECTION already set in this shell.
         Left alone. Whatever exported it (your profile, a parent shell, CI) is trusted as-is.

      2. $env:GRAS_LOCAL_ENV_FILE — a path to a file containing just the connection string
         (one line, no quotes), if you keep it somewhere other than source 3's default path.

      3. $HOME/.gras/pg-test.txt, if it exists.
         RECOMMENDED. This file lives entirely outside the repository, so there is no
         working-tree copy to protect with a .gitignore line and nothing for a scanner, a
         backup tool or an editor's history to ever find inside this checkout. Put the raw
         connection string in it and nothing else needs to change — this script finds it.

      4. The $connectionString assignment a few lines below, in YOUR copy of this file
         (scripts/local-env.ps1). Fallback for anyone who would rather keep the value in this
         file than a separate one. It is still git-ignored, but a value living inside the
         repository directory is one `git add -f`, editor swap file or container build context
         away from leaking — prefer source 3 when you have the choice. Left empty here on
         purpose: see appsettings.Development.template.json for why this template never ships
         a realistic-looking placeholder credential — a fake password that looks real is the
         thing that eventually gets copied into a deployment and quietly works until it does
         not. The shape to paste in, field by field (the same five fields as the example in
         README.md "Running the tests"):

             Host     = <your database host>
             Port     = <your database port, usually 5432>
             Database = schoolmanagement_tests
             Username = <your role>
             Password = <your role's password>

         joined into one semicolon-separated string, Host first and Password last, each as
         "field=value" — the same shape as the worked (non-secret) example already in
         README.md, "Running the tests". Not repeated here as a single literal line on
         purpose: joined together it is exactly the shape a secret scanner looks for, and
         gitleaks correctly cannot tell a real credential from a placeholder in that shape —
         only a human reading this comment can. Splitting the fields like this documents the
         same information without the scanner-shaped string.

.PARAMETER Gate
    The script under this same scripts/ directory to run once POSTGRES_TEST_CONNECTION is
    resolved. Defaults to ci.ps1, so the whole flow — resolve the credential, run every gate —
    is one command.

.PARAMETER GateArgs
    Extra arguments passed through to the gate script unchanged.

.EXAMPLE
    ./scripts/local-env.ps1
    Resolves POSTGRES_TEST_CONNECTION, then runs ci.ps1.

.EXAMPLE
    ./scripts/local-env.ps1 -Gate generate-openapi.ps1
    Resolves POSTGRES_TEST_CONNECTION, then runs a different gate script instead.
#>
[CmdletBinding()]
param(
    [string]$Gate = 'ci.ps1',
    [string[]]$GateArgs = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Source 4. Left EMPTY on purpose — see the long comment above. Paste your own value only into
# YOUR copy (scripts/local-env.ps1), never into this committed template.
$connectionString = ''

$source = $null

if ($env:POSTGRES_TEST_CONNECTION) {
    $source = 'already set in this shell'
}
elseif ($env:GRAS_LOCAL_ENV_FILE -and (Test-Path $env:GRAS_LOCAL_ENV_FILE)) {
    $env:POSTGRES_TEST_CONNECTION = (Get-Content $env:GRAS_LOCAL_ENV_FILE -Raw).Trim()
    $source = "`$env:GRAS_LOCAL_ENV_FILE ($($env:GRAS_LOCAL_ENV_FILE))"
}
else {
    $defaultFile = Join-Path $HOME '.gras/pg-test.txt'
    if (Test-Path $defaultFile) {
        $env:POSTGRES_TEST_CONNECTION = (Get-Content $defaultFile -Raw).Trim()
        $source = 'file at $HOME/.gras/pg-test.txt'
    }
    elseif ($connectionString) {
        $env:POSTGRES_TEST_CONNECTION = $connectionString
        $source = 'the inline $connectionString assignment in this file'
    }
}

# NEVER echo $env:POSTGRES_TEST_CONNECTION here, on any path — only whether it resolved, and
# from which source. Same rule ci.ps1 already follows for this variable.
if ($env:POSTGRES_TEST_CONNECTION) {
    Write-Host "POSTGRES_TEST_CONNECTION resolved from: $source" -ForegroundColor Green
}
else {
    Write-Host 'POSTGRES_TEST_CONNECTION could not be resolved from any of the four sources.' -ForegroundColor Yellow
    Write-Host '  1. already set in this shell           - not set' -ForegroundColor Yellow
    Write-Host '  2. $env:GRAS_LOCAL_ENV_FILE             - not set or file missing' -ForegroundColor Yellow
    Write-Host '  3. $HOME/.gras/pg-test.txt              - not found' -ForegroundColor Yellow
    Write-Host '  4. inline $connectionString in this file - empty' -ForegroundColor Yellow
    Write-Host 'Integration tests will SKIP unless a container runtime is available. See README.md.' -ForegroundColor Yellow
}

$scriptPath = Join-Path $PSScriptRoot $Gate
if (-not (Test-Path $scriptPath)) {
    throw "No such gate script: '$scriptPath'"
}

& $scriptPath @GateArgs
exit $LASTEXITCODE
