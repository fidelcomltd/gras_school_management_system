# Task cards

One file per card, named `TASK-####.md`, following the §4.2 template in the root
[CLAUDE.md](../../CLAUDE.md). Numbering starts at `TASK-0001` and never reuses a number.

Product scope was confirmed on 2026-08-26 by the arrival of `product-specification/`
(revision 3.1). Cards **cite spec section numbers rather than restating requirements**, so a
spec revision does not silently invalidate a card.

Cards are written just ahead of dispatch, not all at once. The full planned sequence lives in
[ROADMAP.md](../ROADMAP.md); a row there without a TASK id is planned, not yet carded.

**Status of record is [STATE.md](../STATE.md) `## In flight`.** This file used to carry a
duplicate status table; it was deleted on 2026-09-04 because it had already drifted, and a fact
with two homes has none.

## Size cap — ~120 lines per card

A card is a brief for one agent, not a transcript (CLAUDE.md §13). The implementing agent reads
its card in full on every dispatch, so length is charged again on every re-dispatch.

When a closed card's `## Log` has outgrown the card, move the log to
[`logs/TASK-####.log.md`](logs/) and leave a pointer. Eight cards were trimmed this way on
2026-09-04; nothing was deleted. `## Goal`, `## Contract delta`, `## Acceptance criteria` and
`## Out of scope` stay in the card — they are what the agent is held to.
