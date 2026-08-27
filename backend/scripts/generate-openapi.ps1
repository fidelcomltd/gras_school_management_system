<#
.SYNOPSIS
    Generates the OpenAPI document from the compiled API. The ONE command for this.

.DESCRIPTION
    Writes the document to backend/artifacts/openapi/SchoolManagement.Api.json, which is git-ignored.

    With -Promote it also copies the result to ../contracts/openapi.json and refreshes
    ../contracts/CONTRACT.lock. That is the only sanctioned way those two files ever change:
    openapi.json is BUILD OUTPUT and is never hand-edited (root CLAUDE.md §3).

    Fully non-interactive: no prompts, no network, no database. Safe to run in CI.

.PARAMETER Promote
    Copy the generated document over ../contracts/openapi.json and update CONTRACT.lock.
    Omit to generate for inspection only.

.PARAMETER Configuration
    Build configuration. Defaults to Release so the committed contract comes from a release build.

.EXAMPLE
    ./scripts/generate-openapi.ps1
    Generate for inspection; the committed contract is untouched.

.EXAMPLE
    ./scripts/generate-openapi.ps1 -Promote
    Regenerate and update the committed contract and lockfile. Review the diff before committing.

.NOTES
    Why the environment variable: generation STARTS the application to read its route metadata, and
    this service validates its configuration at startup — it refuses to run without a database
    connection string. SCHOOLMANAGEMENT_CONTRACT_GENERATION tells startup to skip the checks that need
    live infrastructure. It changes nothing else, and no connection is ever opened. See the HostMode
    class for why this is a flag rather than a placeholder credential.
#>
[CmdletBinding()]
param(
    [switch]$Promote,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$backendRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $backendRoot 'src/SchoolManagement.Api/SchoolManagement.Api.csproj'
$generatedDocument = Join-Path $backendRoot 'artifacts/openapi/SchoolManagement.Api.json'
$contractsDirectory = Join-Path (Split-Path -Parent $backendRoot) 'contracts'
$contractDocument = Join-Path $contractsDirectory 'openapi.json'
$contractLock = Join-Path $contractsDirectory 'CONTRACT.lock'

# Microsoft.Extensions.ApiDescription.Server's GenerateOpenApiDocuments MSBuild target is declared
# Inputs="$(TargetPath)" Outputs="$(_OpenApiDocumentsCache)" — i.e. it re-runs dotnet-getdocument
# (which is what actually writes $generatedDocument) only when the compiled assembly is newer than
# this cache file. Deleting $generatedDocument alone does NOT invalidate that check: the cache file
# is the target's only tracked output, so on an incremental build where the assembly did not need to
# be recompiled, the target is skipped and no document is (re)written at all — proven by running this
# exact sequence: delete the document, leave the cache file, rebuild; the build "succeeds" but the
# document never reappears (the Test-Path check below then throws, which is correct, but the point is
# nothing regenerated it).
# The failure mode this fixes is the inverse and worse: if $generatedDocument was modified by
# something OTHER than this target (hand-edited, restored from a stash, left over from another
# branch/config) while the cache file stayed untouched and current, the target is skipped, and the
# stale/modified file is silently reported below as freshly "Generated" even though the build never
# looked at it. Reproduced 2026-08-26 (TASK-0010): hand-editing the artefact to drop an example, then
# running this script incrementally, left the corrupted 25807-byte document in place and printed
# "==> Generated:" as if nothing were wrong.
# Deleting the cache file here — not just the document — makes the target's Outputs unconditionally
# missing, so dotnet-getdocument runs every single time this script does, regardless of whether the
# assembly changed. The same source therefore always produces the same document; "generated" can no
# longer mean "left over from before" (root CLAUDE.md §3).
$openApiCache = Join-Path $backendRoot 'src/SchoolManagement.Api/obj/SchoolManagement.Api.OpenApiFiles.cache'

if (Test-Path $generatedDocument) {
    Remove-Item $generatedDocument -Force
}
if (Test-Path $openApiCache) {
    Remove-Item $openApiCache -Force
}

Write-Host '==> Generating OpenAPI document' -ForegroundColor Cyan

$previousFlag = $env:SCHOOLMANAGEMENT_CONTRACT_GENERATION
$env:SCHOOLMANAGEMENT_CONTRACT_GENERATION = 'true'

try {
    dotnet build $apiProject `
        --configuration $Configuration `
        -p:GenerateOpenApiContract=true `
        --nologo

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE. The document was not generated."
    }
}
finally {
    # Restored so an interactive shell is not left in generation mode, where startup validation is
    # disabled — that would mask a genuine misconfiguration on the next `dotnet run`.
    $env:SCHOOLMANAGEMENT_CONTRACT_GENERATION = $previousFlag
}

if (-not (Test-Path $generatedDocument)) {
    throw "Expected a document at '$generatedDocument' but none was produced. Check the build output above."
}

$documentInfo = Get-Item $generatedDocument
Write-Host "==> Generated: $($documentInfo.FullName) ($($documentInfo.Length) bytes)" -ForegroundColor Green

# Fail loudly on an empty or pathless document rather than promoting something useless.
$document = Get-Content $generatedDocument -Raw | ConvertFrom-Json
$pathCount = ($document.paths.PSObject.Properties | Measure-Object).Count

if ($pathCount -eq 0) {
    throw 'The generated document contains no paths. Refusing to continue.'
}

Write-Host "==> Document describes $pathCount path(s):" -ForegroundColor Green
$document.paths.PSObject.Properties.Name | Sort-Object | ForEach-Object { Write-Host "      $_" }

if (-not $Promote) {
    Write-Host ''
    Write-Host 'Inspection only — the committed contract was NOT modified.' -ForegroundColor Yellow
    Write-Host 'Re-run with -Promote to update ../contracts/openapi.json and CONTRACT.lock.'
    exit 0
}

Write-Host ''
Write-Host '==> Promoting to the committed contract' -ForegroundColor Cyan

if (-not (Test-Path $contractsDirectory)) {
    New-Item -ItemType Directory -Path $contractsDirectory | Out-Null
}

Copy-Item $generatedDocument $contractDocument -Force

$hash = (Get-FileHash $contractDocument -Algorithm SHA256).Hash.ToLowerInvariant()
$sdkVersion = (dotnet --version).Trim()

# Deliberately NO timestamp: the CI drift check regenerates and requires an empty diff, so anything
# non-deterministic in this file would make that check fail on every run and train people to ignore it.
$lockContent = @"
# CONTRACT.lock — GENERATED. Do not edit by hand.
# Regenerate with: backend/scripts/generate-openapi.ps1 -Promote
openapi.json.sha256 = $hash
generator = Microsoft.Extensions.ApiDescription.Server/10.0.10
dotnet.sdk = $sdkVersion
"@

Set-Content -Path $contractLock -Value $lockContent -Encoding utf8 -NoNewline

Write-Host "==> Wrote $contractDocument" -ForegroundColor Green
Write-Host "==> Wrote $contractLock (sha256 $hash)" -ForegroundColor Green
Write-Host ''
Write-Host 'Review the contract diff before committing. A removed or renamed field is a BREAKING' -ForegroundColor Yellow
Write-Host 'change and needs human sign-off per root CLAUDE.md §3.' -ForegroundColor Yellow
