# 0009 — A provider seam, with environment variables for now

**Status:** Provisional — awaiting a human decision · **Date:** 2026-08-03

## Context

The service needs secrets — at minimum a database connection string. The brief asked for a clean seam
with **one** concrete implementation. The candidates were Azure Key Vault, AWS Secrets Manager and
HashiCorp Vault.

The deployment platform is undecided (ADR 0008). Committing to a cloud SDK now means a realistic chance of
adding a large dependency to the *wrong* cloud, and a future contributor finding Azure packages in a
service that turns out to run on AWS will reasonably assume Azure was chosen.

## Decision

**Ship the seam plus a configuration-backed implementation.**

- `ISecretProvider` in `Application` — `TryGetSecretAsync` / `GetRequiredSecretAsync`.
- `ConfigurationSecretProvider` in `Infrastructure`, reading through `IConfiguration`.

That is deliberately more useful than it first appears, because `IConfiguration` is *already* the layered
chain a secret store plugs into:

```
appsettings.json → appsettings.{Environment}.json → user-secrets (dev) → environment variables → [store]
```

Adding a store is therefore a **one-line change at the composition root**, and this class and every caller
stay untouched:

```csharp
// Azure:  builder.Configuration.AddAzureKeyVault(vaultUri, new DefaultAzureCredential());
// AWS:    builder.Configuration.AddSecretsManager();
// Vault:  builder.Configuration.Add(new VaultConfigurationSource(...));
```

### Rules that come with it

- **Nothing secret is committed** — not a real value, not a placeholder that looks real. A realistic fake
  password is the value that eventually gets copied into a deployment and quietly works.
  `appsettings.Development.template.json` documents every key with empty or non-secret values.
- **Locally: `dotnet user-secrets`**, which stores values in the user profile, outside the repository.
  Not a git-ignored file: even ignored, a working-tree file leaks through backups, editor history,
  container build contexts and the occasional `git add -f`.
- **A secret's value is never logged and never put in an exception message.** Naming the missing *key* is
  helpful; echoing its contents defeats the store. Serilog also redacts credential-shaped property names
  as a backstop.
- **`gitleaks` runs as a CI gate**, so a leak fails the build rather than reaching the remote. A leaked
  credential cannot be un-leaked by deleting it — it stays in history and must be rotated.
- **Configuration that happens to be sensitive** (a connection string, an API key bound to an options
  class) keeps using the options pattern with `ValidateOnStart`, so a missing value fails at boot rather
  than on first use. `ISecretProvider` is for values fetched at runtime by logical name — not an excuse to
  skip writing an options class.

### Known limitation

Configuration providers load at startup, so a secret **rotated** in the store is not seen until reload or
restart. If a rotation window matters, either enable the provider's reload interval or replace
`ConfigurationSecretProvider` with one that calls the store directly — which is exactly what the seam is
for.

## Consequences

**Good.** No cloud dependency chosen prematurely. Works identically in development, test and CI. Adding a
real store touches one line and no consumer.

**Bad.** No concrete cloud provider is shipped, so the brief's "implement one" is only partly satisfied —
recorded in `ASSUMPTIONS.md` as an open item rather than glossed over. Environment variables are visible to
anything that can read the process environment, which is weaker than fetching per use from a vault.

## Revisit when

The deployment platform is confirmed. Then register that platform's configuration provider, update this
ADR, and decide whether rotation latency requires a direct-call implementation.
