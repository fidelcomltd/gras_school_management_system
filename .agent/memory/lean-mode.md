---
name: lean-mode
description: "Since 2026-09-22 the orchestrator writes code directly — no subagent dispatches, minimal ledger, proportionate tests"
metadata: 
  node_type: memory
  type: feedback
  modified: 2026-09-22T03:34:23.698Z
---

On school-management-proj the project lead switched to **lean mode** on 2026-09-22: I write backend and frontend code myself in the
main session, no subagent dispatches unless asked, no card files for small work, one STATE.md line per finished feature.

**Why:** the project lead said the multi-agent workflow was slower than a human developer. Two days bought only ~2.5 features for
millions of tokens, and handoffs, bounces, ledger essays, over-testing and rate-limit recoveries cost more than the code.

**How to apply:** plan briefly in chat, implement, write proportionate tests (happy path + failures that matter), run one
scoped gate on the local container, promote the contract mechanically when it moves, suggest `/code-review` before merge.
Don't reintroduce ceremony; if rigour is needed, make it a mechanical check, not prose. Related: [[token-budget-is-the-binding-constraint]]
