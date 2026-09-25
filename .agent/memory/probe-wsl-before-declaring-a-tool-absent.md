---
name: probe-wsl-before-declaring-a-tool-absent
description: "On this Windows machine the Bash tool is Git Bash, not WSL — two probe methods gave true-but-misleading \"absent\" answers about Docker and cost a wrong decision."
metadata: 
  node_type: memory
  type: feedback
  modified: 2026-09-16T18:45:40.416Z
---

(Recorded on the project lead's Windows dev machine; applies to any Windows machine running Docker inside WSL.) Twice in one session I reported Docker unavailable when it was running fine. Both were method
errors, not machine facts, and the user corrected the second one.

1. **The Bash tool is Git Bash / MINGW64 on the Windows host, NOT WSL** (`$WSL_DISTRO_NAME` unset,
   `uname` = `MINGW64_NT`). It reads the Windows PATH, so `command -v docker` returning nothing is
   a true statement about Windows and says *nothing* about what runs inside WSL. Reach WSL only by
   explicit `wsl.exe -d Ubuntu -- bash -lc "..."`.
2. **`ss -ltnp | grep docker` as a non-root user silently omits root-owned sockets** — `ss` can't
   name the process, so the grep matches nothing and looks exactly like "no listener". `dockerd`
   was listening on `0.0.0.0:2375` the whole time. Use `ss -ltn` and read the ports, or
   `ps -eo args | grep [d]ockerd` to see the actual `-H` flags.

**Why:** I reported "no container runtime anywhere" to the user, who made a real architectural
decision (a docker-compose service) on that bad fact. They knew their own machine and corrected
me. A wrong negative on a capability probe doesn't just lose time — it silently reshapes the plan.

**How to apply:** before telling the user a tool/daemon/port is absent on this machine, check the
*other* side of the Windows/WSL boundary and prefer a positive functional probe over an inventory
one — an HTTP call, a connect, an actual invocation. Here, `Invoke-WebRequest http://localhost:2375/_ping`
settled in one call what two greps got wrong. Note also that WSL2 NAT-mode localhost forwarding
answers on the *hostname* `localhost` only: `127.0.0.1` and raw `[::1]` both fail for the same port.

Project-side facts about this Docker setup live in the repo at `.agent/STATE.md` `## Toolchain`,
which every session is routed to read — that, not this memory, is the durable home for them.
Related: [[token-budget-is-the-binding-constraint]], [[vpn-blocks-postgres-tests]].
