---
name: vpn-blocks-postgres-tests
description: Backend integration test failures on this machine are network, not code - three distinct signatures (VPN port-5432 filtering, transient DNS failure, single-test mid-run TLS drop), each with its own tell and response.
metadata:
  type: project
---

On the project lead's Windows dev machine (2026-09-07), the backend integration suite's collection fixture
(`ApiTestFixture.InitializeAsync`) fails every test with `NpgsqlException: Exception while
reading from stream` / `SocketException 10054` when the Proton VPN tunnel adapter (`ProTUN`) holds
the default route.

Signature that identifies it in under a minute: TCP to `<host>:5432` connects in ~250 ms, the
plaintext PostgreSQL SSLRequest gets **no reply**, and the peer sends RST after ~20 s. TLS to
port 443 on the *same* Neon hostname succeeds in ~600 ms, and an unrelated Postgres provider
(Supabase pooler) on 5432 fails identically — which is what proves it is egress port filtering,
not a Neon outage and not a code regression.

**Why:** this is indistinguishable from the project's known "hosted Neon health" flake at the
gate-summary level (all integration tests red, unit/architecture green), and the ledger already
records multi-minute runs wasted re-running against it.

**How to apply:** when `./backend/scripts/ci.ps1` reports the Integration-tests gate red with every
integration test failing and the unit/architecture gate green, check the default route interface
(`Get-NetRoute -DestinationPrefix '0.0.0.0/0'`) before re-running. Disconnect the VPN, or route
5432 outside the tunnel, then re-run. Do not change test or fixture code for this.

## Second signature, added 2026-09-09: DNS failure, not port filtering

A PARTIAL failure with a different error is the same family and the same response. TASK-0005c's
gate run: **44 of 228 integration tests failed, 42 of them `System.Net.Sockets.SocketException:
No such host is known`** (DNS resolution of the Neon host), ~2 mid-query connection drops, and
**zero assertion failures**. Unit 535/535 and architecture 32/32 green in the same run.

**The duration is the fastest tell.** That suite took **1 h 54 m** against a normal sub-10-minute
run — `EnableRetryOnFailure` backoff, not work. A wall-clock blowout with unit tests green means
network, before you read a single failure message.

**Distinguishing it from the port-filtering signature above:** that one fails EVERY integration
test with `SocketException 10054` after a TCP connect succeeds. This one fails a SUBSET with
`No such host is known` — DNS never resolved, so there was no connection to drop.

**How to apply:** grep the run for `No such host is known` and for assertion failures. If the
failures are all connection/DNS and none are assertions, it is not the card's code — do not touch
test or product code. Verify recovery before re-running, which takes seconds:
`1..6 | %{ [System.Net.Dns]::GetHostAddresses($h) }` plus a `Test-NetConnection -Port 5432`. Only
re-run once resolution is 6/6; re-running into flapping DNS costs another multi-hour run. Also
check for a hung prior run first (a `testhost` with near-zero CPU growth) — see
[[gate-run-serialization]].

## Third signature, added 2026-09-09: a SINGLE test, mid-run TLS drop, no VPN involved

TASK-0049's second gate run: `Failed: 1, Passed: 251, Skipped: 0` — one test, and it was
`RoleEndpointsTests` (unrelated to the card under review). Error was `NpgsqlException: Exception
while reading from stream` / `IOException: An existing connection was forcibly closed by the
remote host` / `SocketException`, thrown inside `ApiTestFixture.ReseedClassLevelsAsync` from
`ResetDatabaseAsync` in `InitializeAsync()` — i.e. **test setup, before any assertion ran**. That
one test took **49 m 29 s** and the suite 1 h 26 m; the identical code had passed 902/902 about an
hour earlier.

**No VPN adapter was up** (`Get-NetAdapter` showed only WiFi and two VirtualBox host-only
adapters), so the port-filtering signature did not apply — and it can't, because that one fails
EVERY integration test, not one.

**How to apply — this is the cheapest of the three.** When exactly one or two integration tests
fail, the error is a transport drop rather than an assertion, the stack sits in fixture
setup/reseed rather than in product code, and the failing test is unrelated to the diff under
review: it is a transient hosted-Neon/WiFi drop. **Confirm the machine is idle (only
`/nodemode:` MSBuild workers, no `testhost`) and just re-run the gate.** Do not reopen the task
card, do not touch fixture or product code, and do not go looking for a defect in the diff — the
tell is that the exception is raised before the test body executes.
