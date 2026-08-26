# Appendix F. Primary Termly Report Sheet Contract

**Design reference:** `assets/04-primary-termly-report-page1.png` (page 1) and `assets/05-primary-termly-report-page2.png` (page 2).

This appendix supersedes `18-appendix-c-result-sheet-contract.md` for pupils in the **Primary** section. Section routing is as described in E.1.

A primary report is **two A4 portrait pages**. Page 1 is the header and the subject table. Page 2 is the two trait blocks side by side, the report and signature block, the fees block and the grade key. It carries attendance, which the nursery sheet does not, and like the nursery sheet it prints no position and no class average.

## F.1 Header block

| Field | Source | Format and example |
|---|---|---|
| document_title | Computed | Two lines, fixed: `Primary Termly Report Sheet` above `Primary Section`. |
| pupil_full_name | Stored | On the `Name` line. Surname in capitals then given names. |
| academic_year | Stored | On the `Year` line. `2026/2027`. |
| pupil_age | Computed | Whole years at the term end date. `8`. |
| session_name | Stored | On the `Session` line. |
| pupil_class | Computed | Level plus arm label by the rule in 6.4.5. `Primary 3A`. |
| next_term_begins | Stored | DD/MM/YYYY from the following term's start date. |
| instructor_name | Stored | The arm's form teacher, captured at publication. |
| days_school_opened | Stored | Integer. Printed against the form's label `Time School Opens`. The label is the school's own wording and is kept; the value is a count of days, not a clock time. |
| overall_grade | Computed | Band letter for the term average. |
| days_absent | Computed | `days_school_opened` minus `days_present`. Printed against `Time Absent`. Never typed. |
| days_present | Stored | Integer. Printed against `Time Present`. |
| term_average | Computed | Two decimal places, on its own line at the foot of the header. `84.98`. |
| revision_notice | Computed, conditional | Present only where revision_number exceeds 1. |

The form omits a `Term` line even though it has `Year` and `Session`. The term is still printed, appended to the class line as `Primary 3A, First Term`, because a sheet that does not say which term it covers is unusable in a file three years later. This is a deliberate addition to the form and is the only field on either sheet that this specification adds rather than transcribes.

## F.2 Academic Assessment of Cognitive Domain

One table under that heading. Structurally identical to the nursery cognitive table, so a single renderer serves both sections.

| Column | Source | Format |
|---|---|---|
| Subject | Snapshot | Subject name in configured order. |
| One column per assessment component | Stored | Generated from the active component list exactly as in E.4. With the GRAS default this produces `1st CA`, `2nd CA` and `Exam`. Reintroducing an assignment adds a column here with no change to this contract. |
| Total | Computed | Integer out of 100. |
| Grade | Computed against snapshot | Band letter. |
| Comment | Snapshot | The band's abbreviation word. |

Nineteen subjects plus a grand total against three component columns fills page 1. A fourth component would still fit; a fifth would push the primary subject table onto a second page and move the trait blocks to page 3, which is acceptable and must not be prevented by a hardcoded page count.

**Grand Total row** sums the 1st CA, 2nd CA, Exam and Total columns, with no grade and no comment.

Note the primary column headers carry no percentage annotation where the nursery ones read `Exam 60%` and `Total 100%`. Both sheets use the same underlying maximums; only the printed heading differs, and each sheet's heading is reproduced as the school wrote it.

### Seeded primary subjects

Mathematics · English Language · Phonics/Diction · Quantitative Reasoning · Verbal Reasoning · Literature · Hand writing · Basic Science/Tech · Social Studies · Health Education · Christian Religious knowledge · Computer Science · Creative Art · Agric Science · Home Economics · Civic Education · History · French · Igbo

Nineteen subjects. Mapped per level per term through 6.6, so lower primary can carry fewer than upper primary without any change here. Nineteen rows plus a grand total fills page 1 on its own, which is why the trait blocks sit on page 2.

**Core subjects.** `25-open-conflicts-to-resolve.md` and Appendix B question 4 ask the school to name the subjects that must be passed for promotion. The subject list above contains both English Language and Mathematics under those exact names, so the provisional default in Appendix B is directly usable.

## F.3 Affective Domain and Psychomotor blocks

Two tables printed **side by side** at the top of page 2, each with three rating columns headed E, I, N. There is no comment column on either, and no fourth rating point: primary uses a three-point scale where nursery uses four. See `25-open-conflicts-to-resolve.md` item 6.

| Field | Source | Format |
|---|---|---|
| block_name | Snapshot | `Affective Domain` and `Psychomotor` as printed. |
| trait_name | Snapshot | One row per configured trait in display order. |
| trait_rating | Stored | One of E, I, N as a mark in the matching column. An unrated trait leaves all three blank and the row still prints. |
| rating_key | Snapshot | Printed once beneath the psychomotor block: `E-Excellent`, `I-Improving`, `N-Needs Improvement`. |

### Seeded affective traits (11)

Conduct · Punctuality · Honesty · Neatness · Attitude · Attentiveness · Co-operation · Skills · Perseverance · Obedient · Fluency

### Seeded psychomotor traits (8)

Sports · Social activities · Painting and drawing · Hand writing · Mathematical Skills · Reasoning · Health · Creativity

These replace the trait lists seeded in 6.2.7, which were guesses made before the school's own form was available. `Hand writing` appears both as a psychomotor trait here and as a subject in F.2; that is the school's own arrangement and is not an error to correct. One is a rated skill, the other is a scored subject, and they are separate records.

## F.4 Report and signature block

The primary form labels these Report rather than comment, and this specification keeps the school's wording on the printed sheet while using the same stored fields as the nursery sheet.

| Field | Source | Format |
|---|---|---|
| teacher_comment | Stored | Free text up to 300 characters, printed against `Teacher's Report`. |
| head_teacher_comment | Stored | Free text up to 300 characters, printed against `Head Teacher's Report`. Required before publication. |
| signature_areas | Snapshot, optional | A `Sign & Date` area beside each line. The head teacher's uploaded signature image renders where present, otherwise a ruled line. |

## F.5 Next-term fees block

Identical in structure, source and behaviour to E.6. The seeded labels are the same six lines, and the school may configure different labels per section: Toiletries and Party Fee are nursery concerns and primary may want other lines.

## F.6 Grade key block

The same nine-band table as E.7, from the same snapshot. Both sections print the identical key because the school uses one grading scale throughout, and both therefore change together when the school edits a band.

## F.7 What the primary sheet does not print

Recorded explicitly so that nobody adds these back in believing them to be missing:

- No position in class, no position in level, no class average, no subject position, no subject highest or lowest, and no number counted. All are computed and stored per `13-result-computation-rules.md`, and all appear on the arm broadsheet in `15-reporting-requirements.md`. See `25-open-conflicts-to-resolve.md` item 3.
- No pupil photograph. Neither of the school's sheets has a photograph frame, which settles by default the question Appendix A entry 39 left open for the portal copy.
- No cumulative three-term block on the Third Term sheet. Appendix A entry 46 adopted one from the Little Angels sample; the school's own sheet has no such block, so the inline cumulative summary is **not printed on these sheets** and the annual cumulative result remains a separate document per 6.7.10. Entry 46 is superseded to that extent, and the reasoning is worth keeping only as a record of why the separate annual document exists.
