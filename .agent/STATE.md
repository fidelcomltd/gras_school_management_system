# Project State

Last reconciled: 2026-09-15 by orchestrator (TASK-0051 closure) · no size cap, see
`## How to read and append to this file` at the bottom.

**This is the ledger. Read it whole — it is meant to be cheap enough to. Then read ONLY what your
card's `Reads:` line names out of `## Index`.**

## Product

**GRAS School Management System** — Golden Royal Ark School (nursery + primary, Nigerian
three-term session). Back office for config, pupils, admission, marks, results and pins, plus one
public page where a parent redeems a reg number + access pin to read a published result.
**Parents have no accounts.**

Authoritative spec: `product-specification/` rev 3.1, entry `index.md` (the school's `.docx` is
rev 1 and is **not** authoritative). Out-of-scope list: `00-document-overview.md` 3.2.

## Index

The router. Every file an agent might need, what it costs, who reads it, and the test for whether
to open it. **If the test does not apply to your card, do not open the file.**

### Rules — binding, one home each

| File | ~KB | Reader | Open it when |
|---|---:|---|---|
| `CLAUDE.md` | 5 | everyone | auto-injected; never open it deliberately |
| `.agent/rules/contract.md` | 5 | orchestrator, contract-guardian; a dev ONLY if `Contract impact` is not `none` | your card moves the contract, or you are running a drift check |
| `.agent/rules/gates.md` | 7 | whoever is about to run or report a gate | before running any gate, or reporting one. **Subagents: read section 1 and stop — you do not run the full gate** |
| `.agent/rules/wire.md` | 1 | both dev agents | always; it is one screen |
| `.agent/rules/auth.md` | 3 | whoever touches auth | your card touches sign-in, sessions, cookies, CSRF, refresh, logout, password or CORS |
| `.agent/rules/governance.md` | 5 | **orchestrator only** | writing or closing a card, reconciling this file, reporting to the human. Never load it into a dev dispatch |

### Specs — the two halves, never both

| File | ~KB | Reader | Open it when |
|---|---:|---|---|
| `.agent/spec/backend.md` (§6) | 3 | `backend-dev` | always, if you are backend-dev |
| `.agent/spec/frontend.md` (§7) | 3 | `frontend-dev` | always, if you are frontend-dev |
| `backend/AGENTS.md` | 10 | `backend-dev` | the concrete recipes behind §6. Section 4 is the endpoint recipe — read that section, not the file |
| `frontend/CONVENTIONS.md` | 17 | `frontend-dev` | the concrete conventions behind §7. Section 4 is the feature-folder layout. Read the section your card names |
| `frontend/HANDOFF.md` | 8 | `frontend-dev` | deliberate omissions and their reasons; only if your card questions why something is absent |
| `backend/docs/ASSUMPTIONS.md` | 108 | `backend-dev` | **NEVER whole.** Five sections: 1 decisions taken on recommendation, 2 decisions taken unasked, 3 open/needs-a-human, 4 defects found by the first real integration run, 5 verified facts. `grep -n "^### "` for the numbered item your card names |
| `frontend/src/api/README.md` | 2 | `frontend-dev` | how the generated seam is shaped |

### Archives — full text behind this file's index lines

| File | ~KB | Open it when |
|---|---:|---|
| `.agent/decisions/2026-Q3.md` | 280 | **NEVER whole.** Your card names a decision; `grep` its TASK id |
| `.agent/decisions/2026-Q3-contract-deltas.md` | 45 | you need an approved contract delta verbatim — auth cookie attributes, CSRF construction and error codes live here |
| `.agent/drift/2026-Q3.md` | 68 | **NEVER whole.** A drift line below is about to be your problem; `grep` its date and subject |
| `.agent/contract-history.md` | 17 | reconciling hash history, or a promote looks out of sequence. The CURRENT hash is in `## Contract` below — that is all a normal dispatch needs |
| `.agent/ROADMAP.md` | 14 | sequencing the next card |
| `.agent/AUDIT.md` | 16 | auditing process compliance |

### Contract

`contracts/openapi.json` is 420 KB. **Never load it.** `jq` the slice your card names:

```
jq '.paths."/api/v1/pupils"' contracts/openapi.json
jq '.components.schemas.PupilDto' contracts/openapi.json
jq '.paths | keys | length' contracts/openapi.json
```

## Layout

```
backend:   ./backend — solution SchoolManagement.slnx. Api / Application / Domain /
           Infrastructure under src/; Unit, Integration and Architecture tests under tests/.
           First real domain is Domain/Auth/ (TASK-0003); /api/v1/reference/* and /health/*
           remain scaffolding.
frontend:  ./frontend — Vite 8 / React 19 / TypeScript 6, package gra-school-portal, npm
           (lockfile committed). Design tokens, Base UI, axios transport in src/lib/http/,
           TanStack Query, Zustand, MSW, react-hook-form + zod, Playwright. Top-level:
           src/api/ app/ components/ config/ features/ lib/ stores/ test/.
           features/<feature>/ is the documented home for screens; features/auth/ is its
           first tenant (TASK-0021). screens/ DELETED 2026-09-06. shared/ EXISTS since 2026-09-14
           (`shared/forms/field-message.ts`, 13 cross-feature importers) — created by the commit
           promoting the first genuinely shared helper, never in advance (CONVENTIONS.md:126-130).
contract generator: Microsoft.Extensions.ApiDescription.Server/10.0.10 via
           backend/scripts/generate-openapi.ps1 -Promote — the ONLY sanctioned way
           contracts/openapi.json and CONTRACT.lock change.
client generator:   openapi-typescript@7.13.0 (types only, pinned exact, devDependency).
           generate-api-schema.mjs writes src/api/schema.d.ts (npm run generate:api);
           check-api-schema-drift.mjs is the drift check-2 gate. src/api/client.ts is the
           hand-written typed request helper over src/lib/http/.
repo:      git, branch main, origin https://github.com/maxcotech/school-management-proj.git
```

## Toolchain present on this machine

dotnet SDK 10.0.100 · node v22.21.0 · npm 10.9.4 · yarn 1.22.22 · pnpm ABSENT · pwsh ABSENT ·
docker ABSENT (integration tests use hosted Neon Postgres via `POSTGRES_TEST_CONNECTION`;
CI uses a service-container Postgres) · psql ABSENT · git 2.51.1.windows.1 · gitleaks 8.30.1

**PINNED — changing one side alone re-breaks CI (0024, 0026):** `global.json`
`rollForward: latestPatch` · gitleaks **8.30.1** in `backend-ci.yml` must equal local · Node
`22.21.0` · **`AnalysisLevel 10.0-All` plus `Microsoft.CodeAnalysis.NetAnalyzers` 10.0.100, forced
over the SDK copy by `backend/Directory.Build.targets`** · `TestingPlatformDotnetTestSupport=false`.
CI prints `dotnet --version`. Re-run the `/analyzer:` check in that targets file after any bump.

## Contract

**Current: `4b31521d833b48f37edeb801815c91f70fc9ff1c42c4ab5fbce04b040512ed29`** · **50 paths** ·
**92 schemas** · api version `v1` · moved 2026-09-15 by TASK-0051 (additive: `POST
/admissions/{id}/approve` and `POST /admissions/{id}/decline`, plus a description-only edit to
`SettingsAbbreviationGroupDto.issuedCount`).

**Corrected 2026-09-15:** this block still named TASK-0055's `7a3c84e6…` / 47 paths / 86 schemas
even though TASK-0062 moved the hash to `de4397b4164d…` on 2026-09-14. Closing TASK-0062 updated
the archive and not this block. Verified against the working tree, not prose.

- `CONTRACT.lock` matches this hash — verified with `sha256sum` 2026-09-15.
- Frontend client is **STALE** against this hash — the two new admission operations are not in
  `src/api/schema.d.ts`. Drift check 2 will be RED until a frontend card regenerates it, the same
  way TASK-0052 and TASK-0054 consumed earlier backend moves.
- The additive classification was verified MECHANICALLY (every existing schema's `required` array
  and every property type diffed against HEAD), not read off the card — see `decisions/2026-Q3.md`.
- `/health/*` is excluded from the document (`ASSUMPTIONS.md` section 2.9); `/reference/*` is
  scaffolding.
- Path counts go stale in prose. The live answer is `jq '.paths | keys | length'`.
- Superseded hashes, and how each shape got its present form: `.agent/contract-history.md`.

## In flight

Open cards only. Closed: TASK-0001–0004, 0006–0029, 0031–0035, 0037–0045, 0047, 0048, 0049,
0050, 0051, 0052, 0053, 0054, 0055, 0059, 0062, 0005a, 0005c. Closure notes: `decisions/2026-Q3.md`.

**Corrected 2026-09-14:** this list previously read `0037–0044`, which silently claimed 0041, 0042
and 0043 as closed while the table below correctly showed them in `review`. Their card headers
also read `Status: done`. Neither was true — `decisions/2026-Q3.md` holds an implemented-to-review
entry for each and **no closure entry**, and TASK-0041's final Log line records nothing committed.
Card headers and this list now both say `review`. TASK-0045 was missing from this table entirely.

**Closed 2026-09-14 (same day):** all four — 0041, 0042, 0043 and 0045 — reviewed against
their acceptance criteria and closed on one orchestrator gate run. **TASK-0045 turned out to be
already implemented in full**, not queued: the reconciliation compared card headers against the
archive and never against the working tree, so an under-claiming header was invisible to it.
`decisions/2026-Q3.md`.

| Task | Title | Owner | Status |
|---|---|---|---|
| TASK-0061 | `GET /pupils` class-progression sort | backend-dev | **queued 2026-09-15** — unblocked by the human ruling: an unenrolled pupil sorts LAST, one flat trailing block. Split from TASK-0059 at its pre-authorised cut line; dispatchable |
| TASK-0060 | Enforce the session boundary in scope decisions | backend-dev | **queued 2026-09-15** — a grant scoped to one session currently authorises against a target in another. Cross-cutting |
| TASK-0058 | Stop an audit-write failure turning a 403 into a 500 | backend-dev | **deferred 2026-09-14** — human priority ruling: critical product features first. Needs a human ruling (403 vs fail-closed) before dispatch |
| TASK-0056 | Emit a machine-readable gate summary file | backend-dev | **queued 2026-09-14** — context-budget pass |
| TASK-0057 | Index-and-archive `backend/docs/ASSUMPTIONS.md` | backend-dev | **queued 2026-09-14** — 108 KB, section 2 alone is 90 KB. Docs only; section numbers are immutable (65 files cite them) |
| TASK-0036 | End-of-session promotion | backend-dev | **blocked** — arms, pupils and enrolments now exist (0059); still needs annual results |
| TASK-0046 | Assignments read surface, rule 2, copy-to-session, 6.1.13 cascades, role archive | backend-dev | **NOT YET CARDED** — split from TASK-0030 on 2026-09-08 but no card file exists. Write it before dispatch (noticed 2026-09-14) |
| TASK-0063 | Registration-number correction and the history alias | backend-dev | **queued 2026-09-15 — UNBLOCKED by TASK-0051** (closed same day). Written in full. Split out of 0051 at its write-up |
| TASK-0064 | Admissions queue screen: approve and decline | frontend-dev | **review 2026-09-15** — client regenerated (drift check 2 GREEN), queue list and Decline fully implemented and tested; Approve shipped as an honest blocked stub, one contract gap found. See card Log |
| TASK-0065 | Make the integration suite runnable without the network | backend-dev | **queued 2026-09-15** — written in full, NOT dispatched. Hosted Neon at ~8s/test makes every gate 45+ min and every network blip a restart; cost TASK-0051 three runs. Human granted the card while ruling product work outranks it |
| TASK-0066 | `GET /admissions/{id}`: make a pending record readable | backend-dev | **DISPATCHED 2026-09-15.** Additive, approved at creation. TASK-0064 is blocked on it: no read path carries `sessionId` or the level id, so the approve dialog cannot build its arm selector |
| TASK-0005b | Logo and signature uploads | backend-dev | queued (stub card) |

Full sequence and cards not yet written: `.agent/ROADMAP.md`.

## Decisions

**Index only — one line per entry.** Full text: `decisions/2026-Q3.md` (grep the TASK id).
Implementation and closure of the same card are merged onto one line.

- 2026-09-15 **TASK-0051 closed.** Registration-number issue and the admission-approval transition;
  counter proven as a row lock, not a max query. 985 tests, Skipped 0, 5 triaged (2 a real stale-
  assertion regression, 3 Neon connection drops re-run green). **The orchestrator caused a gate/agent
  concurrency collision and stopped its OWN gate rather than the agent's DB-touching run.** Contract
  `de4397b4164d…` → `4b31521d833b…`. → `decisions/2026-Q3.md`
- 2026-09-15 **Two drift entries whose trigger named TASK-0051 had not been carried into the card**,
  which was written in full that same morning. The `governance.md` section 3 row 4 grep caught both.
  **Mechanical beats remembered, even for the agent who wrote the card hours earlier.** →
  `decisions/2026-Q3.md`
- 2026-09-15 **TASK-0062 closed, full green gate** (10/10, 953 tests, Skipped 0). `admission_record`
  sections A/I/J. **The orchestrator classified a BREAKING delta as additive** — a NEW required
  request field on `POST /pupils`; caught by contract-guardian at close, human signed off after the
  fact. → `decisions/2026-Q3.md`
- 2026-09-15 **TASK-0051 could not be written as one card: `admission_record` was never built and
  appeared in no dependency list, including its own.** Split three ways — TASK-0062 (the record),
  0051 (approve/decline/issue), 0063 (correction). → `decisions/2026-Q3.md`
- 2026-09-15 **Human ruling on TASK-0061: a pupil with NO open enrolment sorts LAST**, one flat
  trailing block, surname then id; non-null sentinel keys so the widened cursor never carries a
  NULL. Rejected: first, excluded (breaks search), and by-last-closed-enrolment. → `decisions/2026-Q3.md`
- 2026-09-15 **TASK-0059 closed, full green gate.** `enrolment` — the pupil-to-arm spine; the
  one-open-row invariant is a partial unique index proven in BOTH directions. 933/933, Skipped 0,
  contract unmoved. One AC split to TASK-0061. → `decisions/2026-Q3.md`
- 2026-09-15 **A card returned one AC short WITH the open question written down beats one returned
  complete and guessed.** 6.5.15 is silent on where an unenrolled pupil sorts. →
  `decisions/2026-Q3.md`
- 2026-09-15 **Judge a running gate by TWO samples, never one.** The test DB is a Neon POOLER
  endpoint, so an idle `pg_stat_activity` session is a pooled connection, not the worker; and
  `ci.ps1` logs nothing until its summary. The orchestrator misread silence as a hang twice, once
  killing a healthy run and corrupting the database. → `decisions/2026-Q3.md`
- 2026-09-14 **TASK-0053 closed, full green gate.** CSV formula injection neutralised in the audit
  export; neutralise-then-quote ordering is the substance. 917/917, Skipped 0, contract unmoved. →
  `decisions/2026-Q3.md`
- 2026-09-14 **Silence from `ci.ps1`'s integration stage is NOT evidence of a hang.** It logs nothing
  until the final summary, and `ResetDatabaseAsync` runs per test against hosted Neon at ~8s/test, so
  256 tests are 35+ minutes of silence. The orchestrator killed a healthy run, corrupting the shared
  database mid-reseed and costing ~2 hours. **Get evidence a run is stuck before killing it.** →
  `decisions/2026-Q3.md`
- 2026-09-14 **Human priority ruling: critical product features before further audit work.** TASK-0058
  deferred (not dropped; its drift trigger still names it); TASK-0059 (enrolment) opened as the first
  critical card — the stated blocker on TASK-0051, TASK-0036 and two live drift entries.
- 2026-09-14 **TASK-0041, 0042, 0043 and 0045 CLOSED** on one orchestrator gate run — back-office
  shell and guards, academic structure, admins and roles, arms. Contract unmoved. →
  `decisions/2026-Q3.md`
- 2026-09-14 **TASK-0045 was already fully implemented while the ledger called it `queued`.**
  Today's own reconciliation reinstated it as "dispatchable"; dispatching it would have rebuilt
  ~1,800 lines of tested code. **A card's `Status:` is a claim about the TREE — reconciliation
  must grep the code for the card's id before believing it.** Headers drift in BOTH directions,
  and comparing them only against the archive cannot see the under-claiming half. →
  `decisions/2026-Q3.md`
- 2026-09-14 **`src/shared/` appearing is the CONVENTION FIRING, not a violation** — created in
  the commit that promoted the first genuinely cross-feature helper, exactly as
  `CONVENTIONS.md:126-130` prescribes. `## Layout`'s "deliberately absent" line was stale and is
  corrected. → `decisions/2026-Q3.md`
- 2026-09-14 **A drift trigger arrived on TASK-0043 and no acceptance criterion carried it**
  (spec 6.1.2 self-edit). The implementing agent flagged it rather than shipping around it, which
  is `rules/governance.md` §3's second half doing the job its first half missed. →
  `decisions/2026-Q3.md`
- 2026-09-14 **Context-budget pass.** CLAUDE.md 16 KB to 5 KB (router only); STATE.md 250 KB to
  this; the rule sections split to `.agent/rules/` one-file-per-reader; decision and drift full
  text now archived at card close rather than at quarter end. Orchestrator model dropped from the
  1M-context Opus variant. Subagents no longer run the full gate on either side.
- 2026-09-14 **Card sufficiency made the explicit counterweight to the context budget**
  (`rules/governance.md` section 3). Six questions a card must answer before dispatch; a mechanical
  `grep` of this file's drift index replaces the accidental discovery that a 250 KB ledger used to
  provide; `Reads:` is a budget to spend, not a cap to squeeze; and **an implementing agent is now
  required to bounce an underspecified card rather than guess or read everything.** The budget pass
  created this risk and this is what contains it.
- 2026-09-14 **Closed cards' `## Log` sections archived to `.agent/tasks/logs/`** — 25 cards, 2,501
  log lines out of the card bodies, each leaving a pointer plus its closure entry. The convention
  already existed (applied to eight cards on 2026-09-04, then abandoned); this resumes it. Cards
  over ~120 lines fell from 32 to 16, and the remainder is genuine body. **This is what makes
  `rules/governance.md` section 3 row 3 affordable** — a card that says "follow TASK-0049" is now
  pointing at a brief, not a 234-line transcript.
- 2026-09-14 **Ledger reconciled against the card files — three real disagreements found, all
  fixed.** (1) The closed list claimed `0037–0044`, silently including 0041/0042/0043, whose card
  headers ALSO read `done`; the archive holds no closure entry for any of the three and TASK-0041's
  last Log line records nothing committed. All now read `review`. (2) TASK-0022/0024/0025/0026 and
  0031/0032 card headers read `queued`/`review` for work the archive records as closed. All now
  read `done`. (3) **TASK-0045 — a complete, dispatchable frontend card — was referenced by no
  other file in the repo** and would never have been picked up. Now in `## In flight`.
  **Card `Status:` headers had drifted in BOTH directions, so neither side was reliably
  authoritative.** What resolved each case was `decisions/2026-Q3.md` — closure is real only where
  a closure entry exists.
- 2026-09-10 **TASK-0054 closed.** Audit-log client seam typed end to end against `7a3c84e6…`.
  The contract's first non-JSON (`text/csv`) response body resolved `SuccessBody` to `never`;
  fixed with a `ResponseBodyOf` helper, proven by a throwaway type probe before any transport
  code. **Reusable shape: when a shared type-level helper widens, enumerate what can reach the new
  branch — a green suite cannot prove a widening safe.** 350 tests, Skipped 0.
- 2026-09-09 **TASK-0055 closed, first-run pass.** `entityId` filter on both audit operations,
  resolving open question 14 as the human directed. Additive; 47 paths and 86 schemas unmoved.
- 2026-09-09 **TASK-0049 closed after one reopen.** Audit log read surface (`GET /audit-events`,
  cursor-paged, eight optional filters) plus CSV export. Reopened because the export's 200 carried
  no `content`: `.Produces(200, contentType:"text/csv")` supplies no response `Type` and the
  generator silently drops `content`. `.Produces<string>(...)` fixes it. Regression test asserts
  on the generated document.
- 2026-09-09 **Ledger baseline ruling: at closure time the correct baseline is the WORKING TREE.**
  Two HEAD-vs-working-tree false contradictions in one day. Now in `rules/contract.md` section 4.
- 2026-09-09 **TASK-0052 closed.** Client regenerated against `53820aa5…`, consuming 0005c's
  reg-number move and 0050's pupils move in one pass.
- 2026-09-09 **TASK-0050 implemented.** Pupil entity, the pending-exclusion invariant, register
  read surface. Landed 2,452 hand-written lines against a ~1,000 estimate — **the estimate was the
  orchestrator's error, not padding.**
- 2026-09-09 **HUMAN SIGN-OFF: rejected audit events are written on their own connection**, not
  the ambient transaction, departing from spec 14 section 9.3.
- 2026-09-09 **HUMAN SIGN-OFF: `audit_event` append-only is enforced at BOTH layers**, and the
  two-role provisioning is deliberate drift. The migration emits `REVOKE UPDATE, DELETE`.
- 2026-09-09 **TASK-0048 implemented.** `audit_event` persistence and the append-only guarantee.
- 2026-09-09 **TASK-0047 implemented.** Client regenerated against `c3cb88ff…`; all three
  assignment operations reachable through the existing generic wrapper.
- 2026-09-08 **TASK-0044 implemented.** Client regenerated against `84a3444a…`; seven arms
  operations, zero new wrapper code.
- 2026-09-08 **TASK-0041 / 0042 / 0043 implemented, all three to review.** Back-office shell plus
  protected routing plus School Settings; sessions and terms and levels and sections; admin
  accounts and roles. No contract change on any of the three.
- 2026-09-08 **Open question 13 RESOLVED by the human: LEAVE IT.** Spec 6.4.2's rejection messages
  for chain rules 4-plural, 5 and 6 stay unreachable; rule 3's message is accepted for those cases.
- 2026-09-08 **Human priority ruling: product surface comes before further backend modules.**
- 2026-09-08 **TASK-0040 implemented.** Client regenerated against `82870944…`; nine
  sections/levels operations, zero new wrapper code.
- 2026-09-08 **TASK-0038 closed.** Sections, class levels, eight progression-chain rules. Ten
  gates PASS, 594/594, Skipped 0, line 80.55%.
- 2026-09-07 **TASK-0037 implemented.** Client regenerated against `a618db62…`; eight
  sessions/terms operations, zero new wrapper code.
- 2026-09-07 **Open question 6 RESOLVED — root `.gitattributes` added.** The repo now has one
  line-ending policy instead of one that stopped at `backend/`.
- 2026-09-07 **TASK-0034 closed — `contracts/openapi.json` was never reproducible across
  platforms, and gate 10's byte-exact hash was right to say so.** Roslyn writes XML doc files
  platform-dependently; the document is now newline-normalised.
- 2026-09-07 **TASK-0033 — an acceptance criterion was checked off with no test behind it**, found
  by the coordinator reviewing the diff, closed the same session.
- 2026-09-07 **TASK-0028 dispatch 2 closed.** Role entity persisted, five endpoints, rule 2,
  contract `73316bdb…`. The raw-SQL decision reviewed and found sounder than its own justification.
- 2026-09-06 **TASK-0032 closed — gate 9 can now see the diff it is gating.** Two gitleaks passes:
  history, plus `--no-git` over the working tree.
- 2026-09-06 **TASK-0031 closed — the gate invocation stopped being a thing an agent has to get
  right.** `ci.ps1` resolves `POSTGRES_TEST_CONNECTION` itself; the wrapper script is DELETED.
- 2026-09-06 **TASK-0028 SPLIT on a hard entity dependency** and rescoped to roles plus register.
- 2026-09-06 **TASK-0029 closed — typed client seam complete, every gate re-run by the
  ORCHESTRATOR rather than accepted on report.**
- 2026-09-06 **TASK-0027 closed after one reopen on three gaps.** All seven `/admins*` endpoints,
  contract `1a2d8aff…`. Dispatch 2 died on a session rate limit.
- 2026-09-06 **Section 5 human sign-off GRANTED for TASK-0027** (open question 12 closed).
- 2026-09-06 **TASK-0005 delta APPROVED WITH FOUR AMENDMENTS; card SPLIT three ways** —
  0005a identity and ledger, 0005b uploads, 0005c registration numbers.
- 2026-09-06 **TASK-0005a implemented; its dispatch DIED MID-RUN on a session rate limit — the
  THIRD time on this project** (0003, 0019, 0005a). The work was on disk, unverified and
  unrecorded. **Standing lesson: a dispatch that dies after writing and before verifying leaves a
  working tree that looks finished and is not — run the gates yourself before believing a silent
  dispatch.**
- 2026-09-06 **TASK-0021 implemented.** Cookie auth seam plus sign-in and landing screens; bearer
  DELETED, not disabled. Its AC-5 was stale and was amended at dispatch.
- 2026-09-06 **TASK-0019 closed — idempotency substrate shipped, contract-neutral.** 264/264,
  Skipped 0. Proven-not-vacuous on review: a real-concurrency test, and redaction asserted against
  the STORED row rather than the replayed response.
- 2026-09-06 **TASK-0019 delta reviewed; card SPLIT THREE WAYS.** Reviewing the delta against the
  spec rather than against itself found two endpoints missing from a list spec 6.1.14 literally
  enumerates. Full text: `decisions/2026-Q3-contract-deltas.md`.
- 2026-09-06 **STATE.md size cap REMOVED (human directive).** What replaces it is archive
  discipline alone. No agent may defer, trim or skip a STATE.md append to save bytes.
- 2026-09-05 **STANDING LESSONS ON GATES** (0010, 0011, 0015, 0016, 0022–0026). **(1) A check that
  cannot be shown to fail is not a check** — break what it guards, watch it go red, then accept it.
  **(2) A gate is only as trustworthy as the reproducibility of its INPUTS** — before trusting
  green, ask what it reads that is neither committed nor pinned.
- 2026-09-05 **TASK-0022: the gate self-test had NEVER been able to run in CI** — its fixtures
  were `*.trx`-ignored and never committed. Reviewing the DIFF, not the agent's report, found it.
- 2026-09-05 **TASK-0023 / 0024 / 0025 / 0026 closed.** Hermetic vitest `test.env`; SDK, gitleaks,
  analyzer version and test bridge all pinned. **`AnalysisLevel: latest-All` was the real culprit —
  `latest` means "whatever SDK is installed", so the rule set was a property of the machine.**
- 2026-09-05 **Open question 11 RESOLVED (human): follow section 7** — `src/features/<feature>/`,
  react-hook-form plus zod, Playwright; no bulk rename, `src/shared/` waits.

Earlier decisions (bootstrap through 2026-09-04): `decisions/2026-Q3.md`.

## Known drift

**Index only — one line per entry, each with its trigger and owner.** Full text:
`drift/2026-Q3.md`. STRUCK entries are closed and live only in the archive.

### Live — product and spec gaps

- 2026-09-15 **The admissions queue's `missing` column cannot see steps 3 to 8** — contacts, health,
  barred/pickup persons and documents have no entity, so an empty `missing` does NOT mean "ready to
  approve". *Trigger: each step 3-to-8 entity card, and TASK-0051. Owner: `backend-dev`.*
- 2026-09-15 ~~**`headOfSchoolName` has no settings-derived default.**~~ **STRUCK 2026-09-15 by
  TASK-0051** — defaults from `settings.head_teacher_name` at approval, a supplied value still wins,
  proven both ways. The source field was an ORCHESTRATOR RULING (6.5.9 names no setting; 6.2.3 has
  exactly one field of that shape) and is one line to reverse. Full text: `drift/2026-Q3.md`.
- 2026-09-15 **Approval's blocking conditions can only see sections A, B, I and J.** Steps 3 to 8
  have no entities, so approval succeeds against a record 6.5.11 would call incomplete. *Trigger:
  the LAST step-3-to-8 entity card must complete the 6.5.11 gate. Owner: `backend-dev`.*
- 2026-09-15 **`admission` is null on `GET /pupils/{id}`** — only the create response populates it, yet
  6.5.15's detail view names the admission block. *Trigger: the pupil detail-view card. Owner:
  `backend-dev`.*
- 2026-09-15 **The same null-`admission` gap also blocks `GET /admissions`'s own queue rows**,
  which carry `levelAppliedFor` (a name) but no `sessionId`/`classAdmittedInto` (ids) — discovered
  by TASK-0064, which needed both to scope the Approve dialog's arm selector and shipped that
  dialog as a blocked stub instead of shimming a name-match or an "active session" guess. *Trigger:
  a contract card adding session/level ids to the queue row (or a `GET /admissions/{id}`). Owner:
  `backend-dev` for the contract, `frontend-dev` for the follow-up dialog. Full account: TASK-0064
  card Log.*
- 2026-09-09 **`GET /audit-events/export` writes a row on a GET and carries no CSRF token.**
  Spec-mandated by TASK-0049's card, harmless today. **It stops being harmless if the screen
  triggers the download by top-level navigation** — it must use fetch plus blob. *Trigger: the
  audit-log screen card. Owner: whoever writes it.*
- 2026-09-09 **`NigerianGeography`'s 774 LGA names are UNVERIFIED** against an authoritative
  source, and a wrong name can block a real admission (6.5.4 requires a closed list).
  *Trigger: before the first real admission. Owner: `backend-dev` plus human.*
- 2026-09-09 ~~**Arm-scoped `pupil.view` / `pupil.update` is a tested mechanism but a NO-OP against
  today's data.**~~ **STRUCK 2026-09-14 by TASK-0059** — `PupilArmOfRecordLookup` resolves a pupil's
  real arm from their open enrolment; `GetPupilHandler`/`UpdatePupilBiographicalHandler`/
  `ListPupilsHandler` all use it. Full text: `drift/2026-Q3.md`.
- 2026-09-09 **`GET /pupils` default sort omits 6.5.15's class-progression half** — it sorts
  surname then id. **TASK-0059 looked at this and deliberately did NOT build it** — needs a widened
  keyset cursor plus a product decision on where a pupil with no open enrolment sorts, neither of
  which fit that card safely. *Trigger: TASK-0061, unblocked 2026-09-15 by the human ruling it carried (unenrolled pupils sort
  LAST); now queued for dispatch. Owner: `backend-dev`.*
- 2026-09-08 **Spec 6.1.2's self-edit carve-out is not built** — an admin editing their OWN
  `staffName` / `phone` without holding `admin.update`. **Its trigger ALREADY FIRED and was
  missed: TASK-0043 was "the next `/admins` card" and carried no AC for it** (flagged anyway by
  the implementing agent — `admins/api.ts:77`). Needs the ROUTE to accept the narrower
  authorisation before any screen can offer it. *Trigger: the next card touching `PATCH
  /admins/{id}` authorisation, or a human asking why self-service editing is absent.
  Owner: `backend-dev` FIRST, then `frontend-dev`.*
- 2026-09-08 **`DELETE /levels/{id}`'s reference check is PARTIAL and MORE PERMISSIVE than spec
  6.4.2's** "delete only where nothing has ever referenced the row". *Trigger: when history tables
  exist. Owner: `backend-dev`.*
- 2026-09-07 **`POST /terms/{id}/close` does not enforce spec 6.3.6's result-set precondition**
  (blocked by any set in Draft, Awaiting Approval or Approved). *Trigger: the results module.
  Owner: `backend-dev`.*
- 2026-09-06 **`DELETE /roles/{id}` hard-deletes unconditionally, and section 9.4 says it must not
  once assignments exist.** *Trigger: TASK-0046 (role archive). Owner: `backend-dev`.*
- 2026-09-06 **6.1.7 rule 2 cannot be proven end-to-end by a real caller yet** — it needs an actor
  holding `role.update` and not `settings.grading.update`. *Trigger: TASK-0046.
  Owner: `backend-dev`.*
- 2026-09-06 ~~**`abbreviation.issuedCount` will ship as `null` and stay null.**~~ **STRUCK
  2026-09-15 by TASK-0051** — a live count off the pupil table by abbreviation prefix, never the
  counter. Two stale tests asserting `null` were found by the gate and corrected to `0`. Full text:
  `drift/2026-Q3.md`.
- 2026-09-06 **The logo-removal affordance is unrouted** — 6.2.11 requires rejecting "logo deleted
  with no replacement" and 6.2.12 enumerates no `DELETE` route to reject on. *Trigger: TASK-0005b.
  Owner: `backend-dev`.*
- 2026-09-06 **Admin-account audit events are LOG-ONLY**, not the transactional table 6.1.12
  describes. *Trigger: partly superseded by TASK-0048; re-check when the admin module next moves.
  Owner: `backend-dev`.*
- 2026-09-06 **`mustChangePassword` is detected but there is still no change-password screen** — a
  flagged account can only read a message. *Trigger: the card wiring `POST /auth/password` into a
  screen. Owner: `frontend-dev`.*
- 2026-09-06 ~~**The session-end redirect is subscribed per-screen, not once.**~~ **STRUCK
  2026-09-14 by TASK-0041** — `protected-layout.tsx:28` is now the sole production subscriber.
  Full text: `drift/2026-Q3.md`.

### Live — standing obligations on every card

- 2026-09-09 **`before_json` / `after_json` are a STANDING obligation, not a backfill card.**
  `before_json` is never populated; `after_json` carries only the pre-existing `metadata` argument.
  Every card writing an audited mutation must populate both. *Owner: `backend-dev`, every card.*
- 2026-09-09 **`beforeJson` / `afterJson` cross the wire as raw JSON TEXT, not parsed objects** —
  typed `string?` to stop the generator hoisting a shared description-less schema. Every consumer
  must `JSON.parse`. **Accepted.** *Trigger: revisit only if a third `JsonElement` case appears.*
- 2026-09-06 **Any card shipping a retry-duplicable mutation must DECLARE `Idempotency-Key` on
  that route** and mark one-time credentials with `RedactFromIdempotencyReplayAttribute`. Section
  9.8.2's four operations are EXAMPLES, not the list. *Live trigger: TASK-0005.
  Owner: `backend-dev`.*
- 2026-09-04 **The Api-to-Infrastructure arch test exempts `StartupEnvironmentGuard` as well as
  `Program.cs`.** *Trigger: a THIRD exemption must argue for itself or the type gets a port.*

### Live — defects and test gaps

- 2026-09-09 **An unaudited 403 becomes a 500.** `RejectedAuditEventWriter.WriteAsync` is awaited
  with no `try` / `catch` in either `SystemAuditSink.RecordRejectionAsync` or its caller.
  *Trigger: TASK-0058 (re-pointed 2026-09-14 — the old trigger "the next audit card" FIRED on
  TASK-0053 and was deliberately not folded into an encoding-only card). Owner: `backend-dev`.*
- 2026-09-09 **`PrivilegeDecision.cs:21`'s session-bearing-scope-filtering gap, RE-EXAMINED
  2026-09-14 by TASK-0059, still unresolved and no longer a `TODO(TASK-nnnn)` tag.** Enrolment
  existing does not resolve it: pupil routes still bypass `ScopeResolver`/`PrivilegeDecision`
  entirely via the handler-level `PupilAccessGuard` pattern, so `PrivilegeGrant.SessionId` is still
  never exercised by a real caller. The remark was rewritten to stop pointing at TASK-0002 (closed)
  and, since 2026-09-15, reads `TODO(TASK-0060)` — the orchestrator opened that card at TASK-0059's
  closure. **The gap in plain terms: a grant scoped to one academic session authorises the same
  action against a target in another.** *Trigger: TASK-0060. Owner: `backend-dev`.*
- 2026-09-05 **`vite build` succeeds with NO `.env` and emits a bundle that throws on boot**
  (it inlines `VITE_*` as `undefined`). CI copies `.env.example`, which masks it; nothing checks
  env at build time. *Trigger: any card touching build or deployment. Owner: UNOWNED.*
- 2026-09-05 **`gate-summary.tests.ps1:101,:108` are near-unfalsifiable** — `-match` substring
  passes even against a mangled path. *Trigger: the next card touching that suite.
  Owner: `backend-dev`.*
- 2026-09-04 **The DEFAULT rate-limit policy has no 429 test.** *Owner: `backend-dev`, no trigger.*
- 2026-08-27 **Validation is a mediator pipeline behaviour, not section 6's endpoint filter.**
  *Trigger: ratify or revert. Owner: UNOWNED.* `ASSUMPTIONS.md` section 2.2.

### Live — blocked on the deployment decision (open question 5)

One decision clears all four.

- 2026-09-05 **Unpersisted Data Protection key ring** (TASK-0003). *Owner: `backend-dev` plus
  human.*
- 2026-09-05 **`SameSite=Lax` assumes frontend and API share a registrable domain.** Cross-site
  needs `SameSite=None` AND a reconsidered CSRF posture. *Owner: `backend-dev` plus human.*
- 2026-08-26 **Staging and dev-test databases share one role and password.** *Owner: human.*
- 2026-09-05 **Cookie domain undecided.** *Owner: human.*

### Live — build and tooling

- 2026-09-05 **`Microsoft.Testing.Platform.MSBuild` is an unpinned transitive floor, and
  `backend/` has NO NuGet lock file.** Pinning it broke restore. *Trigger: the next `xunit.v3`
  upgrade. Owner: `backend-dev`.*

### Accepted and recorded, no action

- 2026-09-09 **PROCESS INCIDENT, repaired: `contract-guardian` reverted a dev agent's uncommitted
  work** on TASK-0052 closure, then reported the damage its own commands had caused as a check
  failure. Fully repaired. The guardian's prompt now forbids every tree-moving command. *Closed.*
- 2026-09-09 **TASK-0050 landed 2,452 hand-written lines against a ~1,000-line threshold** — the
  ESTIMATE was the orchestrator's error, not padding. *Closed; informs future card sizing.*
- 2026-09-05 **Two accepted auth exposures (TASK-0003):** the bootstrap CLI prints the temp
  password to stdout, and `PersistLockoutStateAsync` has a timing asymmetry. (The third, the DP key
  ring, is under the deployment decision above.) *Owner: `backend-dev`.*
- 2026-09-08 **THE FLAG BYPASS IS GONE.** `SuperAdminFlagEffectivePrivilegeProvider` was deleted by
  TASK-0030 and replaced with `RoleAssignmentEffectivePrivilegeProvider`. *Closed.*

## Open questions

Live only. Resolved questions 1–4 and 6–14 are in `decisions/2026-Q3.md`.

5. **Production database target** undecided; not blocking until deployment. Four live drift
   triggers wait on it (DP key ring, `SameSite=Lax`, shared DB role, cookie domain) — one
   decision clears all four.

## How to read and append to this file

**No size cap** (removed 2026-09-06). Never trim, defer or skip an append to hit a byte target.

**But `## Decisions` and `## Known drift` are INDEXES, and an index entry is ONE line** (two
physical lines if it wraps). That is the format, not a budget. Added 2026-09-14, after the
decisions section reached 187 KB that every agent loaded on every dispatch to learn state that fit
in 8 KB.

**Appending a decision or a drift entry, at card close:**

1. Write the full account into `.agent/decisions/2026-Q3.md` (or `drift/2026-Q3.md`) under a dated
   heading naming the TASK id.
2. Leave ONE line here: date, bolded claim, and — for drift — its *trigger* and *owner*.
3. Never delete. A superseded entry moves to the archive with its resolution recorded.

Rules: `CLAUDE.md` section 3, `.agent/rules/governance.md` section 1.
