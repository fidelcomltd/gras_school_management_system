<#
.SYNOPSIS
    Dependency-free self-test for backend/scripts/lib/contract-ledger.ps1 (TASK-0075).

.DESCRIPTION
    Same hand-rolled harness style as gate-summary.tests.ps1 / postgres-test-connection.tests.ps1 —
    this machine has only the ancient Windows-bundled Pester 3.4.0 and no `pwsh`, so this is
    assertions plus Write-Host, not a Pester suite.

    Dot-sources lib/contract-ledger.ps1 and calls Test-ContractLedger IN-PROCESS against fixture
    files under tests/fixtures/contract-ledger/ — never against the real contracts/CONTRACT.lock,
    contracts/openapi.json or .agent/STATE.md, so this suite cannot touch the real ledger and
    passes identically regardless of the real repo's current state.

    2026-09-05 drift entry (gate-summary.tests.ps1:101,:108) standing lesson: a bare `-match`
    substring check on printed text is near-unfalsifiable — it passed once against a mangled path
    because the substring it looked for was still there. This suite does NOT lean on that shape.
    Test-ContractLedger returns a STRUCTURED result (hashes, counts, and three independent
    booleans), so every assertion below is an exact `-eq` comparison against a value this suite
    computed independently (the fixture's actual, freshly-hashed SHA-256; hand-counted path/schema
    numbers) — not a substring search of a message that happens to still contain the right word.

    Fixture directories (fixtures/contract-ledger/), each self-contained (its own openapi.json,
    CONTRACT.lock — real UTF-8-BOM'd, confirmed by byte inspection — and state-fragment.md
    standing in for .agent/STATE.md):

      valid/               - lock hash, ledger hash and ledger counts all agree. All three
                              booleans true.
      lock-mismatch/        - CONTRACT.lock's recorded hash is wrong; ledger is fine. Only
                              LockMatches is false — proves assertion 1 fails independently of 2/3.
      state-hash-mismatch/  - the ledger's Current: hash is off by exactly ONE character (the
                              scenario the card names explicitly); lock is fine. Only
                              StateHashMatches is false.
      state-count-mismatch/ - the ledger states 3 paths where the document actually has 2; lock
                              and ledger hash are fine. Only StateCountsMatch is false.

    Every fixture's state-fragment.md also carries a decoy paragraph ahead of the real `## Contract`
    block naming "999 paths and 999 schemas", and the real block's own "Previous: ... 99 paths / 99
    schemas" mention — both there to prove the paragraph-bounded parse in
    Get-StateContractLedger picks the CURRENT block's own counts, not the first "N paths" token
    anywhere in the file and not a stale "Previous:" mention in the same paragraph.

    Exits 0 if every assertion passed, 1 otherwise (each failing assertion printed).

.EXAMPLE
    ./scripts/tests/contract-ledger.tests.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$testsRoot = $PSScriptRoot
$scriptsRoot = Split-Path -Parent $testsRoot
$libPath = Join-Path $scriptsRoot 'lib/contract-ledger.ps1'
$fixtures = Join-Path $testsRoot 'fixtures/contract-ledger'

if (-not (Test-Path -LiteralPath $libPath)) {
    Write-Host "FAIL: cannot find contract-ledger.ps1 at '$libPath'." -ForegroundColor Red
    exit 1
}

. $libPath

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

function Assert-Equal {
    param(
        [Parameter(Mandatory)]$Expected,
        [Parameter(Mandatory)]$Actual,
        [Parameter(Mandatory)][string]$Message
    )
    Assert-True ($Expected -eq $Actual) "$Message (expected '$Expected', got '$Actual')"
}

function Get-Ledger {
    param([Parameter(Mandatory)][string]$FixtureName)

    $dir = Join-Path $fixtures $FixtureName
    Test-ContractLedger `
        -LockPath (Join-Path $dir 'CONTRACT.lock') `
        -ContractPath (Join-Path $dir 'openapi.json') `
        -StatePath (Join-Path $dir 'state-fragment.md')
}

# The fixture openapi.json's own true hash, computed here independently (not copied from the
# fixture's CONTRACT.lock or state-fragment.md) — this is the value every -eq assertion below is
# ultimately checked against, so a bug that made every source agree with EACH OTHER but not with
# reality would still be caught.
$fixtureContractPath = Join-Path $fixtures 'valid/openapi.json'
$independentActualHash = (Get-FileHash -LiteralPath $fixtureContractPath -Algorithm SHA256).Hash.ToLowerInvariant()

# ── (a) valid: lock, ledger hash and ledger counts all agree ────────────────────────────────────
$valid = Get-Ledger 'valid'
Assert-Equal $independentActualHash $valid.ActualHash 'valid: ActualHash matches an independently computed hash of the fixture document.'
Assert-Equal 2 $valid.ActualPathCount 'valid: ActualPathCount is the fixture document''s real path count.'
Assert-Equal 3 $valid.ActualSchemaCount 'valid: ActualSchemaCount is the fixture document''s real schema count.'
Assert-Equal $independentActualHash $valid.LockHash 'valid: LockHash read from CONTRACT.lock.'
Assert-Equal $independentActualHash $valid.StateHash 'valid: StateHash read from the Current: token.'
Assert-Equal 2 $valid.StatePathCount 'valid: StatePathCount read from the Current: paragraph, not the decoy "999 paths" earlier in the file or the "99 paths" in the same paragraph''s Previous: mention.'
Assert-Equal 3 $valid.StateSchemaCount 'valid: StateSchemaCount read the same way.'
Assert-True $valid.LockMatches 'valid: LockMatches is true.'
Assert-True $valid.StateHashMatches 'valid: StateHashMatches is true.'
Assert-True $valid.StateCountsMatch 'valid: StateCountsMatch is true.'

# ── (b) lock-mismatch: assertion 1 fails ALONE ───────────────────────────────────────────────────
$lockMismatch = Get-Ledger 'lock-mismatch'
Assert-True (-not $lockMismatch.LockMatches) 'lock-mismatch: LockMatches is false.'
Assert-Equal '0000000000000000000000000000000000000000000000000000000000000000'.Substring(0, 64) $lockMismatch.LockHash 'lock-mismatch: LockHash is exactly the corrupted value from the fixture, not a coincidentally-similar one.'
Assert-True $lockMismatch.StateHashMatches 'lock-mismatch: StateHashMatches is still true - assertion 1 failing does not drag assertion 2 down with it.'
Assert-True $lockMismatch.StateCountsMatch 'lock-mismatch: StateCountsMatch is still true - assertion 1 failing does not drag assertion 3 down with it.'

# ── (c) state-hash-mismatch: assertion 2 fails ALONE (hash off by exactly one character) ────────
$stateHashMismatch = Get-Ledger 'state-hash-mismatch'
Assert-True $stateHashMismatch.LockMatches 'state-hash-mismatch: LockMatches is still true.'
Assert-True (-not $stateHashMismatch.StateHashMatches) 'state-hash-mismatch: StateHashMatches is false.'
Assert-Equal ($independentActualHash.Substring(0, 63) + 'd') $stateHashMismatch.StateHash 'state-hash-mismatch: StateHash is the fixture''s one-character-corrupted value, proving the parser read the actual corrupted text rather than the correct hash from elsewhere.'
Assert-True $stateHashMismatch.StateCountsMatch 'state-hash-mismatch: StateCountsMatch is still true - assertion 2 failing does not drag assertion 3 down with it.'

# ── (d) state-count-mismatch: assertion 3 fails ALONE (ledger says 3 paths, document has 2) ─────
$stateCountMismatch = Get-Ledger 'state-count-mismatch'
Assert-True $stateCountMismatch.LockMatches 'state-count-mismatch: LockMatches is still true.'
Assert-True $stateCountMismatch.StateHashMatches 'state-count-mismatch: StateHashMatches is still true.'
Assert-True (-not $stateCountMismatch.StateCountsMatch) 'state-count-mismatch: StateCountsMatch is false.'
Assert-Equal 3 $stateCountMismatch.StatePathCount 'state-count-mismatch: StatePathCount read the fixture''s wrong value (3), proving the mismatch is real, not a default.'
Assert-Equal 2 $stateCountMismatch.ActualPathCount 'state-count-mismatch: ActualPathCount is still the true value (2), independent of what the ledger claims.'

Write-Host ''
if ($script:failures.Count -gt 0) {
    Write-Host "FAILED: $($script:failures.Count) of $script:assertionCount assertion(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host "PASSED: $script:assertionCount assertion(s)." -ForegroundColor Green
exit 0
