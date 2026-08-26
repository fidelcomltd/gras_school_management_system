# Appendix G. Weekly Report Sheet Contract

**Design reference:** `assets/01-weekly-report-sheet.png`.

The data model behind this sheet is in `20-module-weekly-reports.md`. This appendix fixes what appears on the page and in what order. One sheet covers one pupil for one week and is the same for both sections.

## G.1 Page

| Requirement | Specification |
|---|---|
| Page size | A4 portrait, 12 mm margins. |
| Length | Exactly one page. Five day panels fit within one page at the stated field limits; where a teacher has written to the limit on every line of every day, the sheet flows to a second page and the header repeats. |
| Print fidelity | Fully readable photocopied in monochrome. Panel borders carry the structure. No colour is load-bearing. |
| Generation | Server-side rendering to PDF, cached per pupil per week, invalidated on any edit to that week. |
| File size | Under 120 KB. Lighter than the result sheet because it carries no logo-heavy header and no tables of figures. |
| File name | `GRAS-2026-0041_Week-04_First-Term_2026-2027.pdf`. |

## G.2 Header

| Field | Source | Format |
|---|---|---|
| document_title | Computed | Fixed string `WEEKLY REPORT SHEET`, centred. |
| school_name, school_logo | Snapshot | Printed above the title, smaller than on the result sheet. The paper form has no letterhead; the school's own branding is added so a sheet found loose is identifiable. |
| pupil_full_name | Stored | Printed beneath the title with the class. The paper form omits the pupil's name entirely, which works when the sheet lives in one child's bag and fails as soon as thirty are printed together. This is a deliberate addition. |
| pupil_class | Computed | Level plus arm label. |
| week_label | Computed | `Week 4` and the Monday-to-Friday span, `12/01/2027 to 16/01/2027`. |
| term_name, session_name | Stored | `First Term, 2026/2027`. |

## G.3 Day panels

Five bordered panels in order Monday to Friday. Each panel repeats the same structure.

| Field | Source | Format |
|---|---|---|
| day_name | Computed | `Monday`, centred in the panel's top rule. |
| report_date | Computed | DD/MM/YYYY on the panel's `Date:` line, left. |
| week_number | Computed | On the panel's `Week` line, right. The paper form repeats the week number in every panel and this is reproduced. |
| behaviour | Stored | On the `Behaviour` line. |
| performance | Stored | On the `Performance` line. |
| dressing | Stored | On the `Dressing` line. |
| home_work | Stored | On the `Home Work` line. |
| eating | Stored | On the `Eating` line. |
| symptoms_of_illness | Stored | On the `Symptoms of illness` line. |
| teacher_comment | Stored | On the `Teacher's Comment` line. |
| parent_comment | Stored | On the `Parent's Comment` line. Prints as a blank rule where empty, so a parent can write on the printout. See `25-open-conflicts-to-resolve.md` item 4. |

**Empty lines still print.** Unlike the phone view described in 6.10.9, the PDF prints all eight labels for all five days whether filled or not, matching the paper form and leaving a parent room to write.

## G.4 Rules binding the renderer

1. No renderer computes anything. There is nothing to compute: every value on this sheet is text a person typed.
2. Line order is fixed and identical in all five panels. A panel that reorders its lines to close up gaps is wrong.
3. Text overflowing its line wraps within the panel rather than being truncated. A teacher's note is not clipped to fit a layout.
4. A week with no notes at all does not render. The parent-facing list shows it as `Not available` and no PDF is generated, per 6.10.8.
5. The sheet carries the date and time it was printed. It carries no verification token and no QR code: unlike a result sheet, nobody forges a behaviour note for a secondary school application.
6. No pupil health data beyond the `symptoms_of_illness` line ever appears here. Allergies, medical conditions and medication from the admission form are excluded absolutely, per `25-open-conflicts-to-resolve.md` item 9.
