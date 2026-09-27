# Fresh-session prompt: GRAS hardening cards

> Paste everything below the line into a fresh Claude Code session opened at the repo root. It is written for that
> session (the orchestrator, in LEAN MODE). The lead can read it too: section 3 is the plain explanation of each card
> and why it matters now.

---

Continue GRAS (school management system) in LEAN MODE. This session handles the **hardening cards**. Start by reading
`.agent/STATE.md`; it indexes everything else. Read only the slices each card's `Reads:` line names.

## 1. Where things stand (2026-09-27, end of day)

- **Local `staging` holds everything finished and gate-green.** The lead pushes it; you never push. Check
  `git log staging --oneline -12`: the newest merges are `feat/dashboard`, `feat/reports-final`, `feat/reports-register`,
  `feat/reports-results-3`, `feat/reports-results-2`, `feat/admin-role-assignment`, `fix/session-route-test`,
  `feat/reports-results`, `feat/result-print`. If `git status` shows staging behind `origin/staging`, pull first.
- **Feature scope is essentially complete.** Staff result printing, all 22 reports of spec 15 section 10 (one shared
  `ReportDto` framework: `backend/src/SchoolManagement.Application/Reports/`), the head teacher dashboard on the home page,
  and the admin role-assignment UI are all built. Every module in the spec has a working implementation.
- **`backend-ci` on origin/staging** was red since the promotion merge (a stale route-list test). Fixed on
  `fix/session-route-test`; it goes green once the lead pushes staging.
- **What is left is correctness and security hardening** (the cards below), a few small screens, and getting to
  production. This session owns the cards. Anything else it finds, it records rather than builds.

## 2. The order, and why

| # | Card | Kind | Ruling needed? | Size |
|---|---|---|---|---|
| 1 | **TASK-0060** session boundary in scope decisions | Security | One design choice, yours to make and record | Medium, cross-cutting |
| 2 | **TASK-0068** `GET /pupils` drops a pupil at a page seam | Correctness | **Already ruled: option (b)**, 2026-09-19 | Small to medium |
| 3 | **TASK-0058** audit-write failure turns a 403 into a 500 | Correctness | **Already ruled: fail open**, 2026-09-19 | Small |
| 4 | **TASK-0046** assignments read surface, copy-to-session, cascades, role archive | Completeness | **No card file yet**: write it first, from the spec, then build | Large: split it |
| 5 | **TASK-0056** machine-readable gate summary file | Tooling | None | Small |
| 6 | **TASK-0057** index-and-archive `ASSUMPTIONS.md` | Docs | None | Small, mechanical |

Security first, then silent-data-loss bugs, then the behaviour a real school hits at its first session rollover, then
tooling. One branch per card, `--no-track` from `staging`, merged into LOCAL staging with `--no-ff` when done.

## 3. The cards, explained

### TASK-0060: a grant for one session authorises actions in another (security)

**What is wrong.** Spec 4.2.1 says a privilege check is made "in the session the target belongs to". Every role
assignment carries a `SessionId`, but `PrivilegeDecision` (`backend/src/SchoolManagement.Application/Authorization/
PrivilegeDecision.cs`) ignores it: see the `TODO(TASK-0060)` at the top of that file. So a class teacher assigned Primary
3A in 2025/2026 still holds `result.score.enter` on Primary 3A's rows when a new session's arms or pupils are the target,
as long as the arm or grant matches on other dimensions.

**Why it matters now.** Until today there was no screen to assign roles, so assignments were rare and hand-made. The
admin page now has an Assign role dialog (`frontend/src/features/admins/assignments/`), so assignments will start being
made per session in earnest. The first session rollover after go-live is exactly when last year's grants would wrongly
keep working. It is also the cheapest time to fix it: nobody depends on the lax behaviour yet.

**Shape of the fix.** Filter a grant by the target's session wherever the target is session-bearing (an arm, an
enrolment, a result set). The card asks you to **decide and record** one thing: pupil routes use the handler-level
`PupilAccessGuard` (`Application/Pupils/PupilAccessGuard.cs`) instead of declarative `ScopeParameterKind.Pupil`; either
move them to the declarative path or give `PupilAccessGuard` the session check. Pick one, write the reason into
`.agent/decisions/2026-Q3.md`, apply it consistently. Note that the **reports** (`ReportServices.ResolveScopeAsync` in
`Application/Reports/ReportHandler.cs`) and the dashboard also resolve arm scope through `PupilAccessGuard`: they must
follow whatever you decide.

**Watch for.** It makes authorisation stricter, so existing tests will go red. Treat each as a question: does the test
assert a real requirement, or did it rely on the missing check? **Write the "grant in session A does not authorise
session B" test first and show it red** against today's code. Contract: probably unchanged (metadata only). If the hash
moves, say why.

### TASK-0068: a pupil vanishes from the register when paging (silent data loss)

**What is wrong.** `GET /pupils` orders one group of rows in SQL (database collation) and the next page's same-bucket
comparison in .NET (ordinal). They disagree on punctuation: `Oakes` and `O'Brien` in one arm sort differently, and
O'Brien appears on **no page at all**. Separately, paging into the leavers block loads every unenrolled pupil in the
school's history into memory.

**Why it matters now.** Nigerian surnames with apostrophes and hyphens are common; a register that silently drops a
child is the kind of defect nobody reports because nobody notices. The pupil import (3000-row files are planned) will
fill the leavers block quickly.

**Ruling already given (2026-09-19): option (b)**, do the same-bucket comparison in SQL too, no collation migration.
It runs into the Npgsql translation limit that made TASK-0061 split the query in two (`grep -n "TASK-0061"
.agent/decisions/2026-Q3.md` before touching it), so it may need a raw-SQL fragment or a different query shape. Tests:
a punctuated surname across a page seam, for a class bucket **and** for the leavers block; the leavers fetch bounded.
Contract byte-unchanged. The handoff note from the lead said this card needed a ruling; STATE.md and the card both
record that it has one. Proceed on (b) unless the lead says otherwise.

### TASK-0058: a failed audit write turns a clean 403 into a 500 (correctness)

**What is wrong.** When a privileged request is refused, `PrivilegeAuthorizationHandler` awaits
`auditSink.RecordRejectionAsync(...)` before `context.Fail()`, with no `try`/`catch`. That write deliberately commits on
its own short-lived connection (human sign-off 2026-09-09), so it can fail independently. A transient database fault
therefore turns "you are not allowed" into "server error": the caller learns the endpoint exists, and the denial is
masked in the logs.

**Ruling already given (2026-09-19): fail open on the response.** Return the 403, and log the unwritten audit row at
error level with enough detail to rebuild it. Check the other `RecordRejectionAsync` call sites the card names
(`UpdateAdminAccountHandler`, `CreateRoleAssignmentHandler` twice) and fix each that shares the defect. Test with an
injected throwing writer, **shown red first**. Do not change the durability design (own connection, append-only).

**Why now.** It is small, it is ruled, and the role-assignment UI means rule-1 and rule-3 rejections (which write these
rows) now happen through a real screen.

### TASK-0046: role assignments beyond create and revoke (completeness)

**No card file exists.** It was split from TASK-0030 on 2026-09-08 and never carded. **Write the card first**, from spec
`03-module-admin-roles-audit.md` sections 6.1.5, 6.1.8, 6.1.13 and 6.1.14 and TASK-0030's "Out of scope" list
(`.agent/tasks/TASK-0030.md`), then build it in **two or three branches**, each under about 400 changed lines. The
pieces:

1. **Read surface.** `rolesHeld` / `scopeSummary` on `GET /admins`, and assignments with names on the admin detail. Today
   `RoleAssignmentDto` carries ids only, so the new Roles section resolves names client-side from three lists; a
   names-included DTO removes that and the "name needs role.view" gap.
2. **`POST /assignments/copy-to-session`** with a dry run (6.1.14). Without it, every new session means re-assigning every
   teacher by hand. This is the piece a school hits first, at its first rollover.
3. **6.1.13 lifecycle cascades**: what happens to assignments when a session closes, a role is archived, an arm is
   deleted under an assignment, or a form teacher is reassigned mid-term.
4. **`DELETE /roles/{id}` archive-instead-of-delete** (§9.4), tracked in `## Known drift`.

Already done, so leave it out: escalation **rule 2** (`CreateRoleHandler` and `UpdateRole` enforce it), and the
assign/revoke UI (`feat/admin-role-assignment`). `/auth/me` additions were deliberately deferred by TASK-0030 so the
frontend's consumed endpoint did not change; decide whether they are still wanted and say so in the card. **Do it after
TASK-0060**: copy-to-session is precisely where the session boundary matters.

Contract: additive paths and fields. Promote and verify as in section 4.

### TASK-0056: a machine-readable gate summary (tooling)

`backend/scripts/ci.ps1` prints a `SUMMARY` block but persists nothing, so every agent reading a gate result has to scan
the whole log. The card writes it to `backend/artifacts/gate-summary.txt` on **every** exit path (pass, fail, fail-fast
abort): first line `PASS` or `FAIL`, each gate's outcome, the test totals, the coverage line, and the failing lines in
full. Gitignored. A test in `backend/scripts/tests/` proves the file appears on a failing run. Then the frontend half.
**Why now:** gates are run many times a day and each report currently costs log-reading; this cuts it to one small file.

### TASK-0057: index-and-archive `backend/docs/ASSUMPTIONS.md` (docs)

108 KB, of which section 2 is 90 KB of per-task narratives. Reduce each narrative (2.14 onward) to one to three lines of
"what is now true", and move the full text **verbatim** to `backend/docs/assumptions/2026-Q3.md` under the same section
number. **Section numbers must not move**: 65 files cite them. Sections 1, 4 and 5 are untouched; 2.1 to 2.13 stay as
they are. Prove nothing was lost (archive bytes ≥ removed bytes). **Why now:** every backend change pays for this file in
context whenever it is consulted.

## 4. Binding process (unchanged from the last session)

- **Code and git**
  - You write the code directly (lean mode). No subagents unless the lead asks. A short plan in chat; the cards
    already exist, except TASK-0046 which you write.
  - Branch from `staging` with `git switch --no-track -c <branch> staging`. Merge into LOCAL staging with `--no-ff`.
  - Never push. Never touch `main`. When handing back, tell the lead to run `git push origin staging`: VS Code's Push
    button only pushes the checked-out branch.
  - Commit trailer: `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Contract change, in order**
  1. `backend/scripts/generate-openapi.ps1 -Promote`.
  2. The mechanical additive check against `staging:contracts/openapi.json`. In Git Bash: `MSYS_NO_PATHCONV=1`,
     `tr -d '\r'`, strip `description`/`example`/`examples` with
     `walk(if type=="object" then del(.description,.example,.examples) else . end)`, delete the new paths and new
     schemas from the new side and `.tags` from both, `cmp` the sorted results, and probe each new path non-null. The
     sorted `tags` array may shift. Anything else that moves is breaking: stop and ask.
  3. Update STATE.md `## Contract` (hash, path and schema counts, one "additive verified" line).
  4. `npm run generate:api` then `npm run check:api-drift`.
- **Every new schema** needs an `OpenApiExamples.cs` entry; a nullable enum used only as `T?` also needs a
  `DescriptionsByType` entry.
- **Gates**
  - One scoped backend gate per branch, in the background, nothing CPU-heavy alongside:
    `backend/scripts/ci.ps1 -NoFailFast -IntegrationFilter "FullyQualifiedName~<Area>|..."`. LOCAL container only;
    hosted Neon only if the lead confirms. `Skipped > 0` is a failure. Wait for the completion notice; don't poll.
  - **TASK-0060 is cross-cutting: run a FULL local gate** (`ci.ps1 -NoFailFast` with no filter) before merging it.
    Scoped gates miss other areas' tests: that is exactly how the session-route test broke CI unseen for three merges.
  - Frontend: `npm run verify` green.
  - **Keep WSL awake during gates.** Docker runs inside WSL, and WSL shuts itself down when nothing holds it, which
    makes the gate fail with "NO POSTGRESQL AVAILABLE". Start `wsl.exe -d Ubuntu -- sleep 21600` in the background first,
    then check `curl -s -m 4 http://localhost:2375/_ping` answers `OK` (try `127.0.0.1` if `localhost` does not).
- **Before merging:** `/code-review` on the branch and fix what it confirms; then gitleaks from `backend/`:
  `gitleaks detect --source . --config .gitleaks.toml --redact`, then
  `gitleaks detect --no-git --source .. --config .gitleaks.toml --redact`.
- **Ledger:** ONE short STATE.md `## Decisions` line per card, fixes included: what shipped, rulings, and "Decided
  unasked, needs review:" items. Update the card's `Status:` and the `## In flight` table. Edit STATE.md with node
  scripts in latin1 mode (`fs.readFileSync(p, 'latin1')`, convert your UTF-8 text with
  `Buffer.from(s, 'utf8').toString('latin1')`).
- **Tests:** proportionate, a happy path plus the failures that matter. **Show the defect red first** on 0060, 0068 and
  0058: a check that cannot be shown to fail is not a check.

## 5. Gotchas (learned the hard way; the newest first)

- **Architecture source rules scan text.** Any `.Result.` in production code fails `NoSynchronousBlockingOnTasks`, even
  a tuple field named `Result`: name tuple fields something else. `Random` fails CA5394, even in tests: use a counter.
  Every request needs a FluentValidation validator, even an empty one. Handlers must be sealed; share code through an
  injected service, not a base handler.
- **Npgsql writes only UTC** to `timestamp with time zone`: convert Lagos-offset instants with `.ToUniversalTime()`
  before they reach a query.
- **The `Pupil` query filter hides Pending pupils.** Pending admissions need `IgnoreQueryFilters()` (see
  `PupilRepository`); integration seeds must set pupils Active or they vanish from reads.
- **Phones are stored canonical** (`+234…`); assert on the local digits in tests.
- **`dotnet format` runs the analyzers too**: a Format gate failure can be an analyzer error. Fix formatting with
  `dotnet format --no-restore --include <file>`.
- **The OpenAPI artifact must be newer than every source file** or the architecture tests fail: never edit backend
  source while a gate is running.
- Editing: Git Bash heredocs mangle quotes and backticks; write edit scripts with the Write tool to the scratchpad and
  run them with node. JS `.replace` treats `$$` specially: pass `() => to`.
- Backend: only commands commit, so a handler that writes an audit event must be an `ICommand`. FluentValidation:
  `Cascade(CascadeMode.Stop)` before rules that read a list. "Today" is `WeeklyProjection.LagosToday(timeProvider.GetUtcNow())`.
  Never put names, health text or per-family amounts in audit metadata.
- Frontend: no prettier; single quotes; oxlint forbids non-null assertions, set-state-in-effect, and array-index keys.
  `exactOptionalPropertyTypes` is on: an optional prop that receives `undefined` must be typed `| undefined`.
  MSW is `onUnhandledRequest: 'error'`: mock every request a screen makes. Generated integers are `number | string`.

## 6. Hand-back format

When a card is merged into local staging, give: what was built; the gate summary lines; the "decided unasked" items;
what is left; the push command; and any questions, each with options and a recommendation.

## 7. Not in this session's scope (record, don't build)

These remain from the product list and are for a later session: the config-version history screen, the arm
subject-exceptions screen (spec 6.6.4; the endpoints exist), the admin self-edit carve-out (6.1.2), the parent-portal
soft ceiling, the background import for large files (3000 rows), CSV/PDF export for the four older standalone reports
(weekly completion, illness summary, incomplete records, safeguarding sheet), the spec 6.2.2 setup checklist on the home
page, Cloudinary storage (TASK-0005b stage D, needs the lead's keys), and deployment to the VPS.
