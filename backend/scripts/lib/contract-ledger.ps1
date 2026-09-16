<#
.SYNOPSIS
    Shared logic for the "Contract ledger drift" gate: proves contracts/CONTRACT.lock, contracts/
    openapi.json and .agent/STATE.md's `## Contract` block all agree, instead of trusting them to.

.DESCRIPTION
    Dot-source this file (`. lib/contract-ledger.ps1`) to pull in Test-ContractLedger for use
    inside another script — this is how ci.ps1 uses it, and it is the SAME function
    backend/scripts/tests/contract-ledger.tests.ps1 drives against fixtures, so there is exactly
    one implementation of "does the ledger match", not one in ci.ps1 and a second, subtly
    different one duplicated for tests.

    TASK-0075: `.agent/STATE.md`'s `## Contract` block (the hash, path count and schema count it
    states in prose) had drifted from the actual committed contract THREE separate times
    (TASK-0055->0062, 0062->0063, 0063->0069) because a closing card updated the archive and not
    the block, and nothing checked. Separately, `contracts/CONTRACT.lock`'s recorded hash was
    trusted by every agent and verified by nothing at all. This file checks three independent
    things against the one ground truth — the actual SHA-256 of the committed contracts/
    openapi.json and its actual path/schema counts:

      1. CONTRACT.lock's recorded `openapi.json.sha256` equals the actual hash.
      2. STATE.md's `## Contract` "Current:" hash equals the actual hash.
      3. STATE.md's stated path and schema counts equal the actual document's.

    This file declares no top-level param() block, for the same reason lib/gate-summary.ps1 does
    not (see its own header): dot-sourcing rebinds a param() block on every dot-source and would
    silently clobber an identically-named variable already in ci.ps1's scope. Parsing arguments as
    ordinary function parameters instead means dot-sourcing this file can never touch anything in
    the caller's scope.

    Both CONTRACT.lock and STATE.md are read as raw text and TrimStart([char]0xFEFF) is applied
    before any pattern match — CONTRACT.lock is confirmed (by byte inspection) to start with a
    UTF-8 BOM, the same hazard lib/postgres-test-connection.ps1 documents and strips for
    ~/.gras/pg-test.txt. STATE.md is stripped defensively for the same reason, whether or not it
    currently carries one.

    The STATE.md parse deliberately does NOT assume a fixed line count, a fixed position, or any
    particular trailing prose. It anchors on two textual tokens only, per the card:

      - `Current:` immediately followed by a backticked 64-hex-char hash (`(?s)` so the match
        tolerates the block wrapping across lines — confirmed present in the real block).
      - The first `N paths` and first `N schemas` token found in the PARAGRAPH that begins at that
        `Current:` match (i.e. up to the next blank line, or end of string if none). Bounding to
        that paragraph — rather than searching the whole file — is what keeps a later "Previous:
        `<hash>` / 51 paths / 93 schemas" mention in the same block from ever being mistaken for
        the current counts: prose order guarantees "Current" precedes "Previous", so the first
        match within the Current paragraph is always the current one.
#>

Set-StrictMode -Version Latest

function Get-ContractLockHash {
    <#
    .SYNOPSIS
        Reads CONTRACT.lock's recorded `openapi.json.sha256 = <hash>` line. Throws if the file is
        missing the line entirely (the format itself is unrecognised) rather than returning $null,
        since a caller that got $null back and compared it against a real hash would just report a
        confusing mismatch instead of the actual problem.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$LockPath
    )

    if (-not (Test-Path -LiteralPath $LockPath)) {
        throw "No lock file at '$LockPath'."
    }

    $raw = (Get-Content -LiteralPath $LockPath -Raw).TrimStart([char]0xFEFF)
    $match = [regex]::Match($raw, '(?m)^openapi\.json\.sha256\s*=\s*([0-9a-fA-F]{64})\s*$')

    if (-not $match.Success) {
        throw "Cannot find an 'openapi.json.sha256 = <hash>' line in '$LockPath'. Has CONTRACT.lock's format changed? Regenerate with backend/scripts/generate-openapi.ps1 -Promote."
    }

    return $match.Groups[1].Value.ToLowerInvariant()
}

function Get-ContractDocumentSummary {
    <#
    .SYNOPSIS
        Ground truth for the whole gate: the actual SHA-256 of the committed contract file, plus
        its actual path and schema counts, read straight from the JSON — never from the lock or
        the ledger.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ContractPath
    )

    if (-not (Test-Path -LiteralPath $ContractPath)) {
        throw "No committed contract at '$ContractPath'."
    }

    $hash = (Get-FileHash -LiteralPath $ContractPath -Algorithm SHA256).Hash.ToLowerInvariant()

    $doc = Get-Content -LiteralPath $ContractPath -Raw | ConvertFrom-Json

    $pathCount = 0
    if ($doc.PSObject.Properties.Name -contains 'paths' -and $doc.paths) {
        $pathCount = @($doc.paths.PSObject.Properties).Count
    }

    $schemaCount = 0
    if ($doc.PSObject.Properties.Name -contains 'components' -and $doc.components -and
        $doc.components.PSObject.Properties.Name -contains 'schemas' -and $doc.components.schemas) {
        $schemaCount = @($doc.components.schemas.PSObject.Properties).Count
    }

    [pscustomobject]@{
        Hash        = $hash
        PathCount   = $pathCount
        SchemaCount = $schemaCount
    }
}

function Get-StateContractLedger {
    <#
    .SYNOPSIS
        Reads STATE.md's `## Contract` "Current:" hash and the path/schema counts stated
        alongside it, anchored on textual tokens rather than position — see this file's header.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$StatePath
    )

    if (-not (Test-Path -LiteralPath $StatePath)) {
        throw "No ledger at '$StatePath'."
    }

    $raw = (Get-Content -LiteralPath $StatePath -Raw).TrimStart([char]0xFEFF)

    $hashMatch = [regex]::Match($raw, '(?s)Current:\s*`([0-9a-fA-F]{64})`')
    if (-not $hashMatch.Success) {
        throw "Cannot find a 'Current: ``<hash>``' token in '$StatePath'. Has the ## Contract block's format changed? ORCHESTRATOR owns .agent/STATE.md and must restore that shape."
    }

    $paragraph = $raw.Substring($hashMatch.Index)
    $paragraphEnd = [regex]::Match($paragraph, '\r?\n\s*\r?\n')
    if ($paragraphEnd.Success) {
        $paragraph = $paragraph.Substring(0, $paragraphEnd.Index)
    }

    $pathsMatch = [regex]::Match($paragraph, '(\d+)\s*paths')
    $schemasMatch = [regex]::Match($paragraph, '(\d+)\s*schemas')
    if (-not $pathsMatch.Success -or -not $schemasMatch.Success) {
        throw "Cannot find both an 'N paths' and an 'N schemas' token in the paragraph following 'Current:' in '$StatePath'. ORCHESTRATOR owns .agent/STATE.md and must restore that shape."
    }

    [pscustomobject]@{
        Hash        = $hashMatch.Groups[1].Value.ToLowerInvariant()
        PathCount   = [int]$pathsMatch.Groups[1].Value
        SchemaCount = [int]$schemasMatch.Groups[1].Value
    }
}

function Test-ContractLedger {
    <#
    .SYNOPSIS
        The three assertions, computed against one ground truth (the actual committed contract)
        and returned as a structured result — never as pass/fail text alone — so a caller (the
        gate, or a self-test) can assert on exact values, not on substrings of a printed message.

    .OUTPUTS
        [pscustomobject] with the ground-truth values (ActualHash/ActualPathCount/
        ActualSchemaCount), each source's own recorded value (LockHash/StateHash/StatePathCount/
        StateSchemaCount), and three independent booleans (LockMatches/StateHashMatches/
        StateCountsMatch) — assertion 1, 2 and 3 respectively.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$LockPath,
        [Parameter(Mandatory)][string]$ContractPath,
        [Parameter(Mandatory)][string]$StatePath
    )

    $actual = Get-ContractDocumentSummary -ContractPath $ContractPath
    $lockHash = Get-ContractLockHash -LockPath $LockPath
    $ledger = Get-StateContractLedger -StatePath $StatePath

    [pscustomobject]@{
        ActualHash        = $actual.Hash
        ActualPathCount   = $actual.PathCount
        ActualSchemaCount = $actual.SchemaCount
        LockHash          = $lockHash
        StateHash         = $ledger.Hash
        StatePathCount    = $ledger.PathCount
        StateSchemaCount  = $ledger.SchemaCount
        LockMatches       = ($lockHash -eq $actual.Hash)
        StateHashMatches  = ($ledger.Hash -eq $actual.Hash)
        StateCountsMatch  = ($ledger.PathCount -eq $actual.PathCount -and $ledger.SchemaCount -eq $actual.SchemaCount)
    }
}
