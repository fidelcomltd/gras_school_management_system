---
name: token-budget-is-the-binding-constraint
description: "On the school-management project, Claude usage limits are the binding constraint. Prefer token-cheap approaches even at some cost to thoroughness."
metadata: 
  node_type: memory
  type: project
  modified: 2026-09-25T00:00:00.000Z
---

On `school-management-proj`, Claude weekly usage limits are the real project constraint: in
September 2026 the heavy multi-agent process used up a week's limit in about 3 days. Stay
token-lean: lean mode ([[lean-mode]]) is the way of working.

**Why:** the repo runs a multi-agent orchestration process (orchestrator + backend-dev +
frontend-dev + contract-guardian). Every dispatch re-pays the whole startup context, and every
turn within a dispatch re-sends it. Before the 2026-09-14 context-budget pass, a single
`backend-dev` dispatch loaded ~68k tokens before reading any code, dominated by a 250 KB
`.agent/STATE.md` whose `## Decisions` section was 75% essay-length history.

**How to apply:**
- Prefer the cheap route: `jq`/`grep` a slice over reading a file; one targeted question over a
  broad exploration; direct work over a subagent dispatch when the task is small.
- Before adding process rigour, ask what it costs per dispatch. Rigour that lives in a
  mechanical check is cheap; rigour that lives in prose every agent must read is not.
- When a doc grows, the fix is an index line plus an archive, not a smaller doc.
- Gates are the second-biggest burn — subagents verify their own slice only; the orchestrator
  runs the full gate once per card, backgrounded.
- **2026-09-21, after the user asked why TASK-0088 cost so much:** dev agents run NO integration
  tests and no RED/GREEN proofs (the orchestrator runs both once, in minutes); each stage is ONE
  dispatch of ~400 lines, split at carding; agents commit at every green build. Don't reply to
  idle subagent notifications. Oversized stages were the main burn: ~1.1M tokens for stage A.
- **WSL idles the Ubuntu distro (and Docker) out** once the launching command exits; keep it
  up with a backgrounded `wsl -d Ubuntu -- bash -lc "exec sleep infinity"`.

Related: [[vpn-blocks-postgres-tests]]
