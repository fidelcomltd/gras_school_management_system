---
name: no-native-browser-dialogs
description: "App-wide UI rule from the project lead — never use window.confirm/alert/prompt; confirmations go through the app's own designed ConfirmDialog"
metadata:
  node_type: memory
  pinned: false
  originSessionId: fd36c917-b82d-402c-af09-34ee32379714
  modified: 2026-09-28T05:31:28.335Z
---

The project lead ruled on 2026-09-28, app-wide, that the frontend must never use the browser's native
`window.confirm` (nor `window.alert` or `window.prompt`). They called the native box "ugly": it ignores the app's design
system, cannot be themed, and looks like a browser warning rather than part of the product.

Whenever an action needs the user's confirmation (a delete, a revoke, anything irreversible or with an immediate effect)
use the shared `ConfirmDialog` in `frontend/src/components/ui/confirm-dialog.tsx`, built on the app's own `Dialog`. It
shows a title, an explanation of the consequence, a cancel button, and a confirm button styled for the risk
(destructive for deletes and revokes). It also keeps the dialog open while the mutation runs, so a server error can show
inside it.

**How to apply:** when adding any confirmation step, reach for `ConfirmDialog`; when touching a screen, replace any native
dialog call you find with it. A `grep -rn "window\.\(confirm\|alert\|prompt\)" frontend/src` should stay empty.
