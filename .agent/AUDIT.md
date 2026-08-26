> **SUPERSEDED 2026-08-26.** This audit ran against two empty directories on 2026-07-27 and
> found nothing, correctly, because nothing existed. Both scaffolds have since landed. A real
> audit is carded as [TASK-0001](tasks/TASK-0001.md) and will replace this file when it runs.
> Everything below is kept only as the record of why no audit was produced at bootstrap.

# Audit — 2026-07-27

## Result: no code to audit

Bootstrap step 4 called for auditing both codebases against §6 (backend) and §7
(frontend) and producing a prioritised list of deviations. That audit did not run,
because there is nothing to audit:

```
$ find . -type f | wc -l
0
```

`backend/` and `frontend/` are empty directories. No solution file, no `.csproj`,
no `package.json`, no source files, no CI workflows, no pre-existing `.claude/`
config. Zero files at any depth before this session.

A list of "deviations" against an empty tree would be a restatement of §6 and §7,
not an audit. It is deliberately not produced.

## What was audited instead: the environment

These are real, verified findings that will block specific spec rules once
implementation starts. They are tracked as Open questions 2–5 in
[STATE.md](STATE.md), not as audit items, because they are environment gaps rather
than code deviations.

| Severity | Finding | Rule at risk |
|---|---|---|
| blocker | Not a git repository | §8 Conventional Commits; §4.4 drift-vs-committed-contract |
| blocker | Docker not installed | §6 Testing — Testcontainers required, in-memory provider forbidden |
| should-fix | No Postgres instance identified | §6 Data — EF Core + PostgreSQL |
| nice-to-have | pnpm absent (npm 10.9.4 / yarn 1.22.22 present) | §2 Layout — package manager must be recorded |

## Re-run this audit

Once `backend/` and `frontend/` hold code, re-run against §6 and §7 and replace this
file. The first real audit should happen immediately after the initial scaffold of
each project — that is when a wrong `.csproj` property or `tsconfig` flag is cheapest
to fix.
