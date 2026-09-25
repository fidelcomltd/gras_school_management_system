---
name: integration-db-local-container-first
description: "Integration/gate runs must use the local Testcontainers Postgres; the hosted Neon test DB only after the user explicitly confirms, per run."
metadata: 
  node_type: memory
  type: feedback
  modified: 2026-09-17T16:06:44.546Z
---

Every integration-bearing run (ci.ps1, -IntegrationFilter, raw `dotnet test` on the integration
project) uses the local Testcontainers Postgres on the WSL daemon by default. If the local container
isn't working, stop and ask the user. They'll check the container themselves. Only on their explicit
confirmation may a run use the hosted Neon DB (`~/.gras/pg-test.txt`), and that confirmation covers
that run only.

**Why:** 2026-09-17, a TASK-0076 gate silently resolved `~/.gras/pg-test.txt` (ci.ps1 preferred the
file over the container) and ran against hosted Neon at ~8 s/test, 45+ min instead of ~4, while the
local container was working fine.

**How to apply:** before starting any gate, make sure it won't resolve the hosted file (TASK-0078
adds a `-UseHostedDb` opt-in and makes ci.ps1 fail rather than fall back). Subagents never decide
this; they stop and report. Binding text: `.agent/rules/gates.md` §7. Network-failure signatures that
may show up on a confirmed hosted run: [[vpn-blocks-postgres-tests]].
