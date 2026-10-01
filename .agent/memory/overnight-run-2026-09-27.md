---
name: overnight-run-2026-09-27
description: "Rules for the unattended run from 2026-09-27 — order of work, staging-only merges, spec gaps decided-and-flagged, no hosted DB"
metadata:
  node_type: memory
  type: project
  originSessionId: fbb76384-bf73-441a-a2f8-33aec164785a
  modified: 2026-09-27T00:22:22.385Z
---

The project lead left the orchestrator running unattended on 2026-09-27 (human directive, same day).

- Order: UI PR 2 (`ui/feedback-uploads`), PR 3 (`ui/geography-selects`), PR 4 (`ui/breadcrumbs`), then the remaining features in
  the handover prompt's order (safeguarding sheet, admission slip, completeness column + thumbnail, duplicate search by phone;
  promotion TASK-0036; fee notices; reports module etc.; queued cards incl. TASK-0046).
- Each finished branch is merged into LOCAL `staging` only. Never push, never touch `main`. Each branch stacks on the previous
  one (`git switch --no-track -c <next> <previous>`) so contract promotions never conflict. See [[branch-with-no-track]].
- A spec gap: pick the most conservative option, record it in STATE.md as "decided unasked, needs review", and list it in the
  morning report.
- Local Postgres container down: never use hosted Neon; do frontend-only work and hold backend branches unmerged.
- TASK-0046 has no card: write the card from the spec, flag it for review, then build it.

**Why:** the lead reviews and tests everything on waking, before any PR to main.
**How to apply:** follow these until the lead is back; report every unasked decision.
