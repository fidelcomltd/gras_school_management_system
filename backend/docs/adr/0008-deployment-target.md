# 0008 — Container deployment

**Status:** Provisional — awaiting a human decision · **Date:** 2026-08-03

## Context

The deployment target was not specified, and it affects real choices: health-check shape, how
configuration arrives, and where secrets come from. Something had to be assumed to make those decisions
coherent rather than arbitrary.

## Decision

Assume a **container**, orchestrated (Kubernetes, ECS, Container Apps — the differences do not affect the
application).

Consequences already built in:

**Configuration through environment variables**, which every orchestrator can supply and no container
image needs baked in. `Database__ConnectionString` (double underscore) maps to `Database:ConnectionString`.

**Separate liveness and readiness probes**, and the distinction matters more than it looks:

| Probe | Checks | Orchestrator's response to failure |
|---|---|---|
| `/health/live` | the process only — **no dependencies** | restart the container |
| `/health/ready` | dependencies, including the database | stop routing traffic; do **not** restart |

If liveness also checked the database, a brief database outage would fail liveness on *every* instance
simultaneously and the orchestrator would restart the entire fleet — turning a recoverable dependency blip
into a full cold start, with a thundering herd hitting the database exactly as it recovers. This is a
common and expensive misconfiguration, which is why the split is enforced by
`HealthEndpointTests.Live_RunsNoDependencyChecks`.

Probe responses expose **only** the aggregate status and each check's name and status. The default writer
emits exception messages and durations; these endpoints are necessarily anonymous, so that would hand an
unauthenticated caller server names, connection details and library versions.

**Probes are not versioned** and are excluded from the OpenAPI document. A probe URL is infrastructure
configured in a deployment manifest; moving it to `/api/v2/health` would break every manifest for no
benefit.

**Startup fails fast.** `ValidateOnStart` plus `StartupEnvironmentGuard` mean a misconfigured container
exits at boot rather than starting and failing on the first real request — so a bad rollout halts instead
of half-succeeding.

**Migrations are a separate step**, not applied at startup (ADR 0006).

## Not decided, and deliberately not invented

**There is no Dockerfile.** Base image (`runtime-deps` vs `aspnet`, Alpine vs Debian), non-root user and
UID, whether migrations run as an init container or a pipeline stage, resource limits, and the graceful
shutdown timeout are all deployment decisions with security and operational consequences. Guessing them
would produce a file that looks authoritative and is not.

## Consequences

**Good.** Nothing in the application is cloud-specific, so this assumption being wrong costs little.

**Bad.** If the target turns out to be a Windows service or a bare app-service model, the health-check
split is unnecessary ceremony (harmless) and the environment-variable emphasis may be the wrong ergonomic
choice (minor).

## Revisit when

The human confirms the platform. Then: add the Dockerfile, decide where migrations run, and record the
secret store (ADR 0009).
