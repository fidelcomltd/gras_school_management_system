---
name: two-dev-machines
description: The project lead works from two Windows machines (original + second from 2026-09-25); every repo change must work on both, and machine-local setup must be redone per machine.
metadata:
  type: project
---

Since 2026-09-25 the project lead uses a second dev machine (`hostname` = `PC`, user `HomePC`) and
still uses the original one from time to time. Both must keep working from the same repo.

**Why:** the move surfaced three differences that each broke a gate on the new machine while being
invisible on the old one:
- nvm-windows there has Node 18–26 installed, and its proxy floats a directory with no `.nvmrc` to
  the newest version that satisfies `engines`. Under Node 26, jsdom's `localStorage` is shadowed by
  Node's undefined one, and all 442 frontend tests failed. Fixed by `frontend/.nvmrc` = `22.21.0`.
- The loopback form that reaches the WSL Docker daemon is reversed between the machines. Fixed with a
  `127.0.0.1` fallback in `DatabaseAvailability`, see [[probe-wsl-before-declaring-a-tool-absent]].
- SDK patch differs (10.0.100 vs 10.0.112). `generate-openapi.ps1 -Promote` writes `dotnet.sdk` into
  `CONTRACT.lock`, so that line flips depending on which machine promoted. It's cosmetic, since the
  ledger gate checks the hash. Aligning the SDK patch on both machines ends the churn.

**How to apply:**
- Before blaming code for a failure on one machine only, compare against the per-machine table in
  `.agent/STATE.md` `## Toolchain`. Pin versions in the repo rather than relying on what happens to
  be installed.
- Machine-local things never travel with `git pull`: `~/.gras/pg-test.txt`, .NET user-secrets,
  `appsettings.Development.json` (copy the template), `frontend/.env` (copy `.env.example`, or
  every e2e spec sees a blank page), `.claude/settings.local.json` (`autoMemoryDirectory`),
  `$env:DOCKER_HOST`, the WSL `dockerd` override, and git identity.
- These memories are shared through git. Pull before starting a session, and expect merge conflicts
  in `MEMORY.md` if both machines add memories on different branches.
