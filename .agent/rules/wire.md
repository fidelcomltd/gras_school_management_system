# Wire standards

**This file is CLAUDE.md §8.** Binding in full. Both dev agents read it; it is deliberately short
enough that neither pays for the other's spec to get it.

- **Commits:** Conventional Commits, one logical change each. Task ID in the footer
  (`Refs: TASK-0042`).
- **Timestamps** are UTC `DateTimeOffset` on the wire, ISO-8601 with offset. Timezone conversion
  happens in the UI only.
- **IDs** are opaque strings to the frontend, whatever they are on the backend.
- **Money** is never a float. Minor units as integer, or decimal with an explicit currency code.
- **Enums** cross the wire as strings; the client tolerates unknown members without crashing.
- **PII** never enters logs, analytics, or error reports on either side.
- **Dependencies:** additions require justification in the task card. Lockfiles committed. No
  pinned-to-`latest` anything.
- **No new `TODO`/`FIXME` without a task card number attached.**
