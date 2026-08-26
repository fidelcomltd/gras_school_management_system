# Module 6.10: Weekly Report Sheets

**Design reference:** `assets/01-weekly-report-sheet.png` (Weekly Report Sheet). Read it before building any screen in this module.

## 6.10.1 Purpose

The termly result sheet tells a parent how a child did over thirteen weeks. It arrives once, at the end, when nothing can be done about it. The weekly report is the other channel: a short pastoral note per school day covering how the child behaved, whether they ate, whether they looked unwell, and whether homework came back. It is the thing a nursery parent actually reads, and for a three-year-old it matters more than a mark out of 100.

This module applies to **both sections**. Nursery and primary use the same weekly sheet with the same fields.

Nothing in this module is scored, averaged, graded or ranked. It carries no numbers. That is deliberate: the moment a behaviour note becomes a number it starts being compared, and the value of these notes is that they are specific and small.

## 6.10.2 Shape of the artefact

One sheet covers **one pupil for one week**. The week holds five day panels, Monday to Friday, and each panel holds eight labelled lines:

| Line | Meaning |
|---|---|
| Behaviour | How the child conducted themselves. |
| Performance | How they engaged with work that day. |
| Dressing | Uniform and personal presentation. Nursery staff use this for whether a change of clothes was needed. |
| Home Work | Whether homework was returned and its state. |
| Eating | Whether the child ate. The single most-read line on a nursery sheet. |
| Symptoms of illness | Anything the school observed. Not a diagnosis and not a medical record. |
| Teacher's Comment | The class teacher's free note for the day. |
| Parent's Comment | What the parent wrote back on the paper copy, transcribed by the teacher. See `25-open-conflicts-to-resolve.md` item 4, which is unresolved. |

Every line is free text. None is required. A day with nothing worth saying is left blank and the sheet prints blank lines for it, exactly as the paper form does.

## 6.10.3 Weeks in a term

**A term's weeks are derived, never typed.** The system computes them from the term's `start_date` and `end_date` in 6.3, counting Monday-to-Friday spans:

- Week 1 begins on the term's start date, or on the Monday of that week where the term starts mid-week.
- Weeks run consecutively to the week containing the term's end date.
- A thirteen-week term yields thirteen weeks. A term whose dates change re-derives its weeks, and any week that falls outside the new dates is flagged rather than deleted, because it may already hold notes.
- Each week carries a number and its Monday and Friday dates, so the interface can label it `Week 4: 12/01/2027 to 16/01/2027`.

Public holidays and mid-term breaks are not modelled. A week containing a holiday simply has a day panel with nothing in it, and the school may write Public holiday on the Behaviour line if it wants to. Modelling a school calendar to suppress those cells would be a calendar module, which section 3.2 excludes.

**The weekly report view for a term therefore has one row per week**, which is the requirement the school stated. A term with thirteen weeks shows thirteen rows whether or not any has been filled in.

## 6.10.4 Actors and required privileges

| Operation | Privilege |
|---|---|
| View weekly reports for an arm | `weekly.view`, scopable |
| Enter and edit day notes | `weekly.enter`, scopable |
| Publish or unpublish a week to parents | `weekly.publish`, scopable |
| Delete a week's notes | Not available. Notes are edited, never deleted. |
| View the completion report across arms | `report.view` |

`weekly.enter` and `weekly.publish` are seeded to the Class Teacher role over their own arms, and to School Administrator and Head Teacher school-wide. This answers the requirement that a class teacher, an administrator or a head teacher can all upload this information. The Bursar and Auditor roles get `weekly.view` only.

## 6.10.5 Entity: weekly_report

One row per pupil per week.

| Field | Type | Req | Validation |
|---|---|---|---|
| id | UUID | Yes | System. |
| pupil_id | UUID | Yes | Must be actively enrolled in the arm for the term. |
| arm_id | UUID | Yes | The arm the pupil sat in that week. Stored rather than derived, so a mid-term transfer leaves earlier weeks attributed correctly. |
| term_id | UUID | Yes | The term the week falls in. |
| week_number | Integer | Yes | 1 to 20. Derived per 6.10.3, never typed by a user. |
| week_start_date, week_end_date | Date, Date | Yes | The Monday and Friday. Stored so a printed sheet is reproducible after a term's dates are edited. |
| state | Enum | Yes | draft, published. Draft is invisible to parents. |
| published_at, published_by | Timestamp, UUID | No | Written on publish. |
| created_by, updated_by, updated_at | UUID, UUID, Timestamp | Yes | System. |

Unique on (pupil_id, term_id, week_number).

## 6.10.6 Entity: weekly_report_day

One row per weekday per weekly report. Five rows per report, created together when the report row is created, so the grid always has its cells.

| Field | Type | Req | Validation |
|---|---|---|---|
| id | UUID | Yes | System. |
| weekly_report_id | UUID | Yes | Parent row. |
| day_of_week | Enum | Yes | monday, tuesday, wednesday, thursday, friday. |
| report_date | Date | Yes | The actual calendar date, computed from week_start_date. Printed on the sheet's Date line. |
| behaviour | String 300 | No | Free text. |
| performance | String 300 | No | Free text. |
| dressing | String 300 | No | Free text. |
| home_work | String 300 | No | Free text. |
| eating | String 300 | No | Free text. |
| symptoms_of_illness | String 300 | No | Free text. Where this is non-empty for two or more days in a week, the arm's weekly screen shows a quiet marker beside the pupil, because a child unwell three days running is something the head teacher should see without reading thirty sheets. |
| teacher_comment | String 500 | No | Free text. |
| parent_comment | String 500 | No | Free text, transcribed by staff. Never written by an unauthenticated portal request under the provisional resolution in `25-open-conflicts-to-resolve.md` item 4. |

Unique on (weekly_report_id, day_of_week).

No field here is ever required, and there is no completeness gate of the kind results have in 6.7.5. A weekly report is a courtesy, not a record the school is held to, and blocking a teacher's Friday on eight lines times thirty children would guarantee the feature goes unused.

## 6.10.7 Entry screen

The paper form is one pupil per page. The screen is not.

**Primary entry surface: one arm, one week, one field at a time.** The teacher picks the week, then picks a line such as Eating, and gets a grid of pupils down the side and the five weekdays across the top, with a text cell in each. She fills Eating for the whole arm for the whole week, then switches to Behaviour. This is the pattern that makes the feature survive contact with a Friday afternoon: the teacher is in one frame of mind, writing one kind of note, thirty times.

The following affordances are required, because without them staff will not complete this:

- **Fill across.** Type once, apply to every remaining weekday for that pupil. Most children ate normally every day.
- **Fill down.** Apply a value to every pupil for that day. Used for whole-class facts: `Class went on excursion`.
- **Phrase memory.** The field offers the values that account has previously typed into the same field this term, most recent first, as tappable suggestions. Not generated text, not a fixed list. Just what the teacher already wrote, offered back.
- **Autosave** every thirty seconds and on cell blur, with the same unsaved-work banner and retry behaviour specified in 9.8.2 for score entry. The resilience requirement is identical and for the same reason.
- **Per-pupil view** as a secondary tab, showing one pupil's whole week as the paper form lays it out, for a teacher who prefers to work child by child and for checking before publishing.

Both surfaces write the same rows. Neither is a different feature.

## 6.10.8 Publication

A week is published per arm per week, not per pupil, and publishing makes every pupil's row for that week visible on the portal.

- Publishing requires `weekly.publish`. No approval chain, no head teacher gate, no computation. This is a lighter process than result publication on purpose.
- A published week can be unpublished by the same privilege, with no reason required, because the stakes are a behaviour note rather than a grade.
- Editing a published week is allowed and takes effect immediately. Unlike a result, there is no revision number and no parent-facing revision notice: a corrected behaviour note is a corrected note, not a reissued document.
- A week with no content in any pupil's row cannot be published: **Nothing has been written for Week 4 yet. Add at least one note before publishing.**
- **Auto-publish option**, off by default and settable per arm: publish each week automatically at 17:00 on its Friday. A school that trusts its teachers turns this on and removes a step; a school that wants a look first leaves it off.

## 6.10.9 Parent view on the portal

The weekly report is reached inside an existing viewing session. It consumes **no additional pin use**, per 6.8.8, because it is the same pupil in the same session.

The term selector in 6.9.2 gains a second row per term. Where a term previously offered only its result, it now offers:

- `First Term result` with its existing availability states.
- `First Term weekly reports` with a state of Available where at least one week is published, or Not available yet.

Selecting weekly reports lands on **a list with one row per week of that term**, numbered and dated, each row showing whether it is published and a one-line preview drawn from the teacher's comment on the most recently filled day. Unpublished and empty weeks are visible and disabled, with the label `Not available`, following the same rule as unavailable terms in 6.9.2: a parent who cannot see Week 7 at all assumes the site is broken.

Opening a week shows the five day panels in the paper form's order and wording. On a phone the panels stack vertically, one day per card, with empty lines omitted from the card rather than printed as blank rules, because a phone screen of eight empty labels is noise. The printed and downloaded PDF keeps every line whether filled or not, matching the paper form.

A `Download PDF` action produces the A4 weekly sheet per `23-appendix-g-weekly-report-contract.md`. Downloading consumes no use.

## 6.10.10 Error and edge cases

| Case | Behaviour |
|---|---|
| Pupil joins the arm in Week 6 | Weeks 1 to 5 have no rows and show as Not available to the parent. No blank rows are back-filled, because the child was not there. |
| Pupil transfers to another arm in Week 6 | Weeks 1 to 5 stay attached to the old arm through the stored `arm_id`. Week 6 onward is entered by the new arm's teacher. The parent sees a continuous list of weeks and is not shown the arm change. |
| Pupil withdrawn mid-term | Existing weeks remain. No further weeks are created. Portal access follows the withdrawn-pupil rule in 6.8.12. |
| Term dates edited after notes exist | Weeks are re-derived. A week now outside the term is flagged on the arm screen as `Week 14 falls outside the term's new dates` and its notes are retained and still visible. Nothing is deleted. |
| Teacher writes in a week that has not started | Allowed. A teacher preparing Monday's note on Sunday evening is not an error. |
| Two staff edit the same cell at once | Last write wins, per 9.5. The field shows the other account's name and time beneath it when it was changed within the last hour. |
| Symptoms of illness recorded three days running | The arm screen and the head teacher's dashboard show a marker. No alert is sent, because section 3.2 excludes messaging. |
| Parent's Comment contains something a teacher should not have transcribed | Editable by `weekly.enter`. The audit log holds the previous value, per 6.1.12. |

## 6.10.11 Endpoints

| Endpoint | Notes |
|---|---|
| GET /terms/{id}/weeks | The derived week list for a term. No stored week rows needed to answer this. |
| GET /arms/{id}/weekly?term_id=&week_number= | The whole arm's grid for one week, pupils by weekdays, in one response per 9.8.2. |
| PUT /arms/{id}/weekly | Bulk upsert of the grid. Accepts sparse payloads so a Fill down writes only the cells it touched. Idempotency key required. |
| GET /pupils/{id}/weekly?term_id= | One pupil's whole term, week by week. Backs the per-pupil tab and the parent list. |
| POST /arms/{id}/weekly/{week_number}/publish | Requires `weekly.publish`. |
| POST /arms/{id}/weekly/{week_number}/unpublish | Requires `weekly.publish`. |
| GET /portal/weekly?term_id= | Published weeks for the pupil the viewing session is bound to. Consumes no pin use. |
| GET /portal/weekly/{week_number}/pdf | The A4 weekly sheet. Cached per pupil per week. |
| GET /reports/weekly-completion | Per arm per week: pupils with at least one note, cells filled of cells available, published state. |

## 6.10.12 Reporting

Two additions to `15-reporting-requirements.md`:

- **Weekly report completion.** One row per arm per week for the active term: pupils with any note, total cells filled against cells available, published state, last edited by and when. This is the report that tells a head teacher on a Monday morning which teachers did not write anything last week.
- **Illness observation summary.** One row per pupil per term where `symptoms_of_illness` was recorded on two or more days, with the dates and the text. Restricted to `report.view` plus the safeguarding privilege in `07-module-pupils-guardians.md`, because it is health observation about a child and should not sit in a general export.
