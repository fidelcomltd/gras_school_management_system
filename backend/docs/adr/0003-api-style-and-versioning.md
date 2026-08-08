# 0003 — Minimal APIs, grouped into modules, with URL-segment versioning

**Status:** Accepted · **Date:** 2026-08-03

## Context

Two choices: controllers versus Minimal APIs, and how to version. The root `CLAUDE.md` §6 mandates
Minimal APIs grouped with `MapGroup`, and §3 mandates URL-segment versioning `/api/v{n}/...`. Both are
therefore settled; what needed deciding was *how* to keep them workable.

Minimal APIs concentrate everything in `Program.cs` by default, which becomes an unreviewable file and a
permanent merge-conflict site. And URL-segment versioning has a specific, easily-missed interaction with
OpenAPI generation.

## Decision

**Endpoint modules.** Every feature implements `IEndpointModule`, discovered by assembly scanning.
Adding a feature's endpoints is a one-file change with no registration list to edit — so two people
adding features never conflict in a shared file.

**URL-segment versioning** via `Asp.Versioning.Http`. The group is
`MapGroup("/api/v{version:apiVersion}")` with a version set and `ReportApiVersions`, so responses carry
`api-supported-versions` and a client can discover that its pinned version is going away.

**A document transformer substitutes the concrete version into the contract.** This is the part worth
recording. The router needs the template `v{version:apiVersion}`, and the generator faithfully emits
that template as the path — so the document would contain `/api/v{version}/reference/ping`. The frontend
generates a typed client from this document, and it would either produce a method taking a pointless
`version` argument or call the literal URL `/api/v%7Bversion%7D/...`.

`VersionedPathDocumentTransformer` rewrites the paths to `/api/v1/...` and removes the now-meaningless
`version` path parameter. `OpenApiContractTests` asserts no `{version}` survives, so the fix cannot be
silently removed.

**One OpenAPI document per version.** A future v2 gets its own document and its own generated client.

## Consequences

**Good.** Real runtime versioning, and a contract with exact paths a generator can use. `Program.cs`
stays a composition root. A new endpoint needs no wiring step.

**Bad.** The transformer is machinery that exists to work around a tooling interaction, and someone
unfamiliar will wonder why it is there — hence the long comment in it and this ADR. Shipping v2 means
adding a second document and extending the transformer to be version-aware.

## Alternatives considered

- **A literal `/api/v1` group.** Simpler, exact paths for free. Rejected: `UrlSegmentApiVersionReader`
  reads the version from the route value, so with a literal segment the versioning library contributes
  almost nothing and version reporting stops working.
- **Header or media-type versioning.** Cleaner URLs, but harder to test with a browser or `curl`, and
  invisible in logs and dashboards. Also contradicts root `CLAUDE.md` §3.
- **Controllers.** More ceremony per endpoint, and contradicts root `CLAUDE.md` §6.
