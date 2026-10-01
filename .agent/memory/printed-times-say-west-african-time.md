---
name: printed-times-say-west-african-time
description: "Times shown to people (especially on PDFs) are marked WAT, never \"(Lagos)\", in the one form \"Sept 30, 2026, 9:58pm WAT\""
metadata:
  node_type: memory
  pinned: false
  originSessionId: 9fb8cbf8-c227-4141-b8bd-1ec399ef4fc2
  modified: 2026-09-30T21:13:41.071Z
---

The project lead ruled on 2026-09-30 that the app must never label a time as "(Lagos)". Readers took it to mean the school is in Lagos, which it is not. Lagos is only the IANA time-zone id (`Africa/Lagos`, UTC+1 all year), and that id can stay in code and configuration.

Every time shown to people, printed or on screen, uses one form: "Printed Sept 30, 2026, 9:58pm WAT". That is short month names (with "Sept", "June" and "July"), a 12-hour clock with lowercase am/pm, and "WAT". The lead first asked for "(West African Time)" in full. Once the result sheet's narrow footer made that wrap, the lead said not to break a stamp onto a second line but to find a shorter form, and then chose "WAT" for every print.

Use the shared helpers rather than a hand-written format string: on the backend `SchoolTime.Stamp` (and `SchoolTime.ClockStamp` for a time alone) in `SchoolManagement.Application/Common`, which is also the only home of the UTC+1 offset; on the frontend `schoolDateTime` and `schoolClock` in `src/shared/format/date.ts`.
