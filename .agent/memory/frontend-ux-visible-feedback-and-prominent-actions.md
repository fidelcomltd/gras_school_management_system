---
name: frontend-ux-visible-feedback-and-prominent-actions
description: "App-wide UI rules from the project lead — server errors must be visible whatever the scroll position, and a screen's key action must be visually prominent"
metadata:
  node_type: memory
  pinned: false
  originSessionId: 7e420186-afc2-46c4-bfee-af7ef13ef58f
  modified: 2026-09-28T00:04:01.003Z
---

The project lead set two app-wide frontend rules on 2026-09-28, after using the deployed app.

**Feedback from the server must be visible wherever the user is on the page.** In a long form, a validation or error
message returned after pressing the submit button was rendered at the top of the form, so the user did not know anything
had happened until they scrolled back up. Any place a backend error or validation message is shown must be in view at
the moment it appears: pinned (sticky) to the top of the scrolling area, or scrolled into view and focused. Apply this to
every new form and dialog, not only the one that prompted it.

**The action that matters most on a screen must be obviously noticeable.** On the Subjects screen the "map them to
classes" action was a quiet button the lead could not find "despite it staring me in the face". Primary and
workflow-critical actions get primary styling and a prominent position, not an outline or ghost button or a text link
buried in body copy. The lead asked for this app-wide, so check prominence whenever a screen gains an important action.

**Why:** the lead is the first real user of the deployed app and these were real moments of confusion; school staff are
not power users.

**How to apply:** when building or touching a screen, ask "if a server error comes back, will the user see it without
scrolling?" and "is the most important next step the most visible control here?"
