# Shared Claude Code auto-memory

Claude Code's auto-memory for this repo lives here so every teammate's sessions share it.
`MEMORY.md` is the index Claude loads at session start (first 200 lines / 25 KB); each other
`.md` file holds one memory. This README is not loaded.

## One-time setup (each teammate, each clone)

`autoMemoryDirectory` must be an absolute path, so it can't be committed. Add it to your own
`.claude/settings.local.json` (gitignored), using your clone's path:

```json
{ "autoMemoryDirectory": "C:/path/to/school-management-proj/.agent/memory" }
```

Restart Claude Code. New memories are then written here and reviewed in PRs like any other file.

## What belongs here

Project facts, team conventions, and lessons that save the next session time. Write for a
teammate: say "the project lead", not "the user", for decisions only they made. Personal
preferences, account details, and anything secret go in `~/.claude/CLAUDE.md` instead. Never
put credentials here.
