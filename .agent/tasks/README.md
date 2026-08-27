# Task cards

One file per card, named `TASK-####.md`, following the §4.2 template in the root
[CLAUDE.md](../../CLAUDE.md). Numbering starts at `TASK-0001` and never reuses a number.

Product scope was confirmed on 2026-08-26 by the arrival of `product-specification/`
(revision 3.1). Cards **cite spec section numbers rather than restating requirements**, so a
spec revision does not silently invalidate a card.

Cards are written just ahead of dispatch, not all at once. The full planned sequence lives in
[ROADMAP.md](../ROADMAP.md); a row there without a TASK id is planned, not yet carded.

| Card | Title | Owner | Status |
|---|---|---|---|
| [TASK-0001](TASK-0001.md) | Re-audit both scaffolds against §6/§7 | reviewer | queued |
| [TASK-0002](TASK-0002.md) | Privilege register and authorisation enforcement | backend-dev | done |
| [TASK-0003](TASK-0003.md) | Admin accounts, authentication, session management | backend-dev | queued |
| [TASK-0004](TASK-0004.md) | OpenAPI client generator and typed API layer | frontend-dev | done |
| [TASK-0005](TASK-0005.md) | School settings: identity, reg number, config versioning | backend-dev | queued |
| [TASK-0006](TASK-0006.md) | Regenerate the frontend client against the TASK-0002 contract | frontend-dev | done |
| [TASK-0007](TASK-0007.md) | Clear the SSH.NET High advisory blocking the vuln gate | backend-dev | queued |
| [TASK-0008](TASK-0008.md) | Give the integration-test connection string a durable local home | backend-dev | queued |
| [TASK-0009](TASK-0009.md) | Move the document-property contract tests off the database | backend-dev | done |
| [TASK-0010](TASK-0010.md) | Make OpenAPI document generation deterministic | backend-dev | in-progress |

Current status of record is [STATE.md](../STATE.md) `## In flight`, not this table.
