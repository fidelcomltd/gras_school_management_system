# contracts/

Per §3 of the root [CLAUDE.md](../CLAUDE.md), this directory holds the single source
of truth for the HTTP boundary:

- `openapi.json` — generated from the backend build. **Never written by hand.**
- `CONTRACT.lock` — SHA-256 of `openapi.json` plus generator tool versions.

## Neither file exists yet

The backend does not exist, so there is nothing to generate from. No placeholder was
created on purpose: a hand-written stub `openapi.json` would make all four §4.4 drift
checks pass against a fiction, which is worse than having no contract at all.

Both files appear in the same commit as the first backend build that emits a document.
Until then, `frontend/src/api/` cannot be generated and no boundary-touching task card
can be dispatched.
