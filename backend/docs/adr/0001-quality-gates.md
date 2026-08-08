# 0001 — Warnings are errors, and conventions are enforced by tooling

**Status:** Accepted · **Date:** 2026-08-03

## Context

This codebase will be edited by many actors, human and AI, most of whom will not have read the
conventions. Any rule that depends on a reviewer noticing will be broken, then normalised, then
defended.

Warnings in particular decay predictably: one is noise, fifty is background, and by then a real one is
invisible.

## Decision

1. `TreatWarningsAsErrors=true` repo-wide, in `Directory.Build.props`. No project overrides it.
2. `AnalysisLevel=latest-All` — every .NET analyser rule on — with a curated relaxation list in
   `.editorconfig`, **each entry carrying a written justification**.
3. `EnforceCodeStyleInBuild=true`, so formatting and style violations fail the build, not just
   `dotnet format`.
4. One extra analyser package set (Roslynator). Not three.
5. **Central Package Management**: versions live only in `Directory.Packages.props`, so drift between
   projects is structurally impossible.
6. Anything expressible as an architecture test **is** one — see the enforcement table in
   `CONTRIBUTING.md`.
7. `GenerateDocumentationFile=true` repo-wide, with `CS1591` (missing XML comment) an **error** in `Api`
   and `Application`, because those comments become the OpenAPI descriptions.

## Consequences

**Good.** A convention violation fails the build with a message, at the moment it is introduced, for
everyone. Nobody has to be the person who enforces style in review.

**Bad.** The build is strict in ways that occasionally feel pedantic. A new analyser rule arriving with
an SDK update can break the build with no code change — accepted, because the alternative is pinning
analysers and never getting new checks.

**Deliberately excluded:** `NU190x` (vulnerable transitive package advisories) are *not* build errors.
A newly published advisory would otherwise break every developer's build with no available fix. They
are a dedicated CI gate instead: still blocking, but visible and in one place.

## Alternatives considered

- **Warnings as warnings, enforced in review.** Rejected: this is the default state of every codebase
  that ends up with thousands of warnings.
- **`latest-Recommended` instead of `latest-All`.** Milder, but the relaxations then become invisible
  defaults rather than documented choices. The curated list is more work up front and more honest.
