# CLAUDE.md

@AGENTS.md

## Claude Code specifics

- This package sits inside the GRAS monorepo; the repo-root `CLAUDE.md` and `.agent/STATE.md` still apply
  (branching, never pushing, one STATE.md line per finished feature).
- Seeing your work: `npm run build && npm run start`, then `npm run screenshots -- --routes /about --widths 390,1440`
  and **Read** the PNGs in `.screenshots/` against the canvas artboards. Describe differences before claiming fidelity.
- Styles stale in `next dev` after editing `app/globals.css`? `npm run dev:clean` (Turbopack cache on Windows).
