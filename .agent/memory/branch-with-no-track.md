---
name: branch-with-no-track
description: "Create feature branches with --no-track; a branch started from origin/main tracks main, and an IDE push then lands on main"
metadata:
  node_type: memory
  type: feedback
  originSessionId: fbb76384-bf73-441a-a2f8-33aec164785a
  modified: 2026-09-26T23:47:14.869Z
---

Create every feature branch with `git switch --no-track -c <branch> origin/main` (or branch from the local `main`), never plain
`git switch -c <branch> origin/main`.

**Why:** on 2026-09-26 `ui/shell` was created from `origin/main`, so Git set its upstream to `origin/main`. It was left checked
out, and a push from the IDE ("Sync Changes") sent the branch straight to `main` at 00:33 on 2026-09-27, ahead of `staging` and
without a PR. That breaks the rule that main changes only with the project lead's consent through a PR.

**How to apply:** after creating a branch, confirm `git rev-parse --abbrev-ref <branch>@{upstream}` errors (no upstream) or names
`origin/<branch>`. Never leave a branch whose upstream is `origin/main` checked out. See [[lean-mode]].
