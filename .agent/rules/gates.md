# Gates — who runs what, and what counts as evidence

**This file is CLAUDE.md §9.** Binding in full. Read it when you are about to run or report a
gate; you do not need it to write code.

Consolidated here 2026-09-14 from CLAUDE.md §9 and the `## Gate commands` block of STATE.md,
which every agent was loading on every dispatch whether or not it was going to run a gate.

---

## 0. Gate scope — the full suite is CI's job, not the orchestrator's

**Human directive, 2026-09-16.** Running `ci.ps1` end to end once per card cost ~1 hour of wall
clock regardless of card size, because the integration stage is 310 tests against *hosted* Neon at
~8s each. Two cards could consume an afternoon for a few hundred lines of business value. That is
no longer the default.

**What changed underneath:** `.github/workflows/backend-ci.yml` now triggers on `staging` as well
as `main`. It always did the full run properly — a `postgres:17.6-alpine` **service container**
local to the runner, no TLS-over-internet, plus its own explicit skipped-tests-are-failures check
— but it was watching a branch nobody worked on, so it never fired. The local hour was substituting
for a CI job that was simply switched off.

| Situation | What runs | Roughly |
|---|---|---|
| Per card, locally, before close | **Scoped gate**: every cheap gate in full, plus the integration tests the card actually touches | minutes |
| Per push to `main`/`staging` | **Full `ci.ps1` in CI**, all 310 integration tests, authoritative | minutes, off this machine |
| Before a release, or after a cross-cutting change | Full `ci.ps1` locally, deliberately | ~1 hour |

**The cheap gates still run IN FULL every card, no exceptions** — restore, format, build
`-warnaserror`, unit + architecture tests, OpenAPI generation, contract drift, vulnerable
dependencies, secret scan. Together they are a few minutes and they catch most of what breaks.
What is now scoped is the integration stage alone.

**The honest trade:** a scoped local gate can miss a cross-cutting integration regression that a
full run would catch. CI is what closes that hole, so **CI actually running is load-bearing, not a
nicety.** A card may close on a green scoped gate, but if its CI run then goes red, that is a
reopen, not a footnote. If CI is broken or disabled, the local full gate comes back until it is not.

**Judgement, not a formula.** A card touching one endpoint gets the scoped gate. A card touching
authorisation, the cursor/pagination substrate, the DbContext, a migration that rewrites existing
rows, or anything every query passes through gets the full local run — the blast radius, not the
diff size, decides. When genuinely unsure, run it full and say why.

**`ci.ps1` has no scoping flag yet** (only `-NoFailFast`, `-AllowSkipped`, `-SkipContractDrift`), so
the scoped gate is assembled by hand today — see TASK-0067, which adds `-SkipIntegration` and
`-IntegrationFilter` so it stops being assembled by hand. Until it lands, §5's counts rule matters
more than ever: **a hand-rolled `dotnet test --filter` skips silently and still exits 0.**

## 1. Who runs the full gate

**Subagents do NOT run the full gate. Either side. Ever.**

| Agent | Runs | Does NOT run |
|---|---|---|
| `backend-dev` | `dotnet test --filter` over what it touched; `dotnet build -warnaserror` on the projects it changed | `backend/scripts/ci.ps1` |
| `frontend-dev` | `npm run typecheck`, `npm run lint`, `npm run test -- <path>` over what it touched | `npm run verify`, `npm run test:e2e`, `npm run check:api-drift` |
| `orchestrator` | the **scoped** gate once per card per §0 (full local run only when §0's blast-radius test says so), `run_in_background: true`, reviewing the diff while it runs | — |

**Rationale (backend, 2026-09-09, after it cost THREE dispatches; extended to the frontend
2026-09-14).** The backend suite is 700+ tests plus a Release build, coverage merge, gitleaks over
the whole history and an OpenAPI regeneration, and no longer fits the tool's 600000 ms ceiling — a
foreground call times out showing NOTHING, the subagent ends its turn, and the orchestrator pays a
full round trip for a result that was always going to arrive late. The frontend case is not the
timeout, it is the bill: `verify` + `test:e2e` per dispatch re-runs Playwright over the whole app
to check a three-file change, and the orchestrator re-runs it anyway before closing. One run, one
owner.

Do not "helpfully" run the full gate from a subagent to save a step. That is the behaviour this
rule exists to stop, and on the backend two concurrent runs corrupt the shared Neon database (§4).

**A subagent reports COUNTS over its own slice, and stops.** That is a complete report, not a
half-done one. The card is closed on the orchestrator's run, never on the subagent's.

## 2. The canonical backend invocation

Copy it verbatim, do not improvise a variant. One `PowerShell` tool call,
`run_in_background: true`, from the repo root:

```
./backend/scripts/ci.ps1 -NoFailFast
```

`ci.ps1` is 10 gates, cheapest-first, and STOPS at the first failure unless `-NoFailFast`.
`Failed > 0` or `Skipped > 0` exits non-zero (the latter unless `-AllowSkipped`). It ends in a
`SUMMARY` block. CI: `.github/workflows/backend-ci.yml`.

Four rules, each of which cost a wasted multi-minute run on 2026-09-06 (TASK-0028 dispatch 1):

1. **`run_in_background: true`, NOT a foreground `timeout`.** It exceeded the 600000 ms ceiling on
   2026-09-09 (TASK-0005c) and the foreground call returned nothing. Background it and read the
   SUMMARY from the completion notification.
2. **Never `2>&1`.** Windows PowerShell 5.1 wraps a native command's stderr in
   `NativeCommandError` and sets `$?` false, so a run with all ten gates green reports exit 1.
   `dotnet` writes to stderr routinely.
3. **Never `| Select-String`.** It hides the failure reason, so the next attempt is a guess. The
   `SUMMARY` block is already the short form.
4. **Never `cd` first.** The tool's working directory is already the repo root and `ci.ps1` does
   its own `Push-Location`. A leading `cd` also breaks the permission prefix match, so the call
   prompts instead of matching the allowlist.

**No env-var prefix, ever.** Since TASK-0031 `ci.ps1` resolves `POSTGRES_TEST_CONNECTION` itself
from `~/.gras/pg-test.txt` (BOM-stripped; an explicitly set variable still wins). `local-env.ps1`
and its template are DELETED — there is no wrapper any more, and there must not be a new one.

## 3. The frontend commands

```
npm run verify            # typecheck, lint, test, build   — ORCHESTRATOR ONLY
npm run check:api-drift   # §4.4 check 2, deliberately NOT folded into verify
npm run test:e2e          # Playwright, own CI job         — ORCHESTRATOR ONLY
npm run generate:api      # regenerate the client when the contract moves
```

oxlint, not ESLint. CI: `.github/workflows/frontend-ci.yml`.

## 4. Gate runs must be strictly serialized

Added 2026-09-08 (TASK-0038) after it cost THREE wasted verifications. There is ONE hosted Neon
test database shared by every runner on this machine, and `ResetDatabaseAsync` truncates every
table then reseeds. Two overlapping runs interleave truncate-and-reseed against the same rows.
Never start a `ci.ps1` while another is live — not yours against a subagent's, not two subagents'.

**The signature, so it is recognised on sight instead of diagnosed from scratch:** a
`23505 duplicate key` on a seeded table's primary key AND `Sequence contains no elements` for a
seeded row, *in the same run*. Those two are contradictory — one says the seeded rows are present,
the other that they are absent — and a single writer cannot produce both. Concurrent builds
separately give `MSB3021`/`MSB3026`/`MSB3027` copy-lock errors with no `CS####` anywhere: a
collision, not a compile failure.

**Checking for idleness:**

```
Get-Process -Name dotnet, testhost, testhost.x86, vstest.console, MSBuild
```

A `Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'"` check does NOT see `testhost.exe`
and will call a machine quiet while test hosts still hold the database — that exact hole caused
the third wasted run and produced a REOPEN of a card whose diff was fine. Idle MSBuild worker
nodes (`MSBuild.dll /nodemode:1 /nodeReuse:true`) are harmless and expected; a `testhost` or a
live `dotnet test` / `dotnet build` command line is not.

## 5. What counts as evidence

**Paste the summary line plus every failing line in full — never a whole log.** For the backend
that is the `SUMMARY` block alone. `Failed: 0, Passed: 178, Skipped: 0` and the coverage line are
the evidence; restore chatter is not.

**A skipped suite is not a passing suite.** Gates exit non-zero on `Skipped > 0`. This project's
costliest defect was a false close on a silently skipping suite.

**A subagent reporting success without pasting its slice's counts has not finished.** Re-dispatch.

**Cheapest gate first, stop at the first failure.** A broken typecheck should surface in seconds,
not after a Postgres spin-up.

## 6. The full gate set (CLAUDE.md §9)

Run by the orchestrator before a card closes, and by CI on every push:

```
Backend:   dotnet build -warnaserror
           dotnet format --verify-no-changes
           dotnet test
Frontend:  npm run typecheck
           npm run lint
           npm run test
           npm run build
Contract:  regenerate openapi.json → diff must be empty
           regenerate src/api      → diff must be empty
           CONTRACT.lock hash matches
```

## 7. Which database an integration run uses — local container first, hosted only on human confirmation

**Human directive, 2026-09-17. Binding on every agent, every run: `ci.ps1`, `-IntegrationFilter`, and
any raw `dotnet test` over the integration project.**

1. **Default is the local Testcontainers Postgres** on the WSL daemon (`http://localhost:2375`).
2. If the container path is not working (daemon unreachable, container fails to start), **STOP and
   ask the human.** They will check the local container. Do not diagnose around it, retry against
   something else, or "just run it" on hosted.
3. **The hosted database (Neon, `~/.gras/pg-test.txt`) is used ONLY after the human confirms, for that
   run.** A confirmation does not carry over to later runs or later cards.
4. Never export `POSTGRES_TEST_CONNECTION` to a non-local host to get a run through. CI's own
   service-container value is the only sanctioned explicit connection.
5. **A subagent never makes this call.** It stops and reports "local container unavailable". The
   orchestrator asks the human.

**Why:** on 2026-09-17 a TASK-0076 gate silently resolved `~/.gras/pg-test.txt` and ran ~370
integration tests against hosted Neon (us-east-2, ~8 s/test, 45+ min) while this project's ledger said
local runs had been container-backed since TASK-0065. The container path was working the whole time.

**Enforced by `ci.ps1` since TASK-0078 (2026-09-17):** the hosted file is read only with `-UseHostedDb`, which the
orchestrator passes only after the human confirms for that run. Outside CI, an explicit non-local connection string
fails the gate. A failed container-backed integration stage prints the ask-the-human guidance.
