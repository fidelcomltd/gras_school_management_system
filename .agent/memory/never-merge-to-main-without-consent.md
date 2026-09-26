---
name: never-merge-to-main-without-consent
description: "Never merge or push anything into main without the project lead's explicit consent; merging finished branches into staging is allowed"
metadata:
  node_type: memory
  type: feedback
  originSessionId: a5da418c-37e0-461c-82bf-6245630197d9
  modified: 2026-09-26T05:15:48.249Z
---

Never merge a branch into `main`, and never push to `main`, without the project lead's explicit consent for that branch.
Merging a finished, reviewed branch into `staging` IS allowed (project lead, 2026-09-26: "never merge branches to main without
my consent, instead you can do with staging").

**Why:** on 2026-09-26 two deploy branches reached `main` by direct push with no PR, and the project lead understood it as
the agent merging silently. (The reflog showed pushes from the shared clone after the agent's turns had ended; the agent had
run no `git push`. The rule stands regardless.) `main` is what production deploys from, via the manual `deploy-production`
workflow.

**How to apply:** finish a branch (gate, gitleaks, `/code-review`), commit, then hand back and ASK before anything touches
`main`. Staging merges are fine, but the repo still blocks agent pushes to `staging` by design (agent rule, `settings.json`
deny, `pre-push` hook), so a staging merge stays local until the project lead pushes, or lifts that block. When writing
walkthroughs, never phrase a step so it reads as "merge into main" unless the project lead asked for it; say "open a PR".
Related: [[lean-mode]], [[two-dev-machines]]
