# Design Reference Images

Eight images were supplied by the school. Unlike the earlier Little Angels sample, **these are Golden Royal Ark School's own forms**. They are authoritative. Where any of them contradicts a rule written earlier in this specification, the image wins and the contradiction is recorded in `25-open-conflicts-to-resolve.md`.

All files live in `assets/` beside this file. A frontend agent building any of these screens should open the image, not work from the prose alone: the prose fixes the data, the image fixes the arrangement.

| # | File | Title | Governs | Spec file that specifies it |
|---|---|---|---|---|
| 1 | `assets/01-weekly-report-sheet.png` | Weekly Report Sheet | Per-week, per-weekday pastoral report. Five day panels (Monday to Friday), each with eight labelled lines. | `20-module-weekly-reports.md`, `23-appendix-g-weekly-report-contract.md` |
| 2 | `assets/02-nursery-termly-report-page1.png` | Nursery Termly Report Sheet, page 1 | Nursery header block and the first three development domains (Maths Readiness, Language/Communication, Personal & Physical), rated E/S/I/N with a per-indicator comment column. | `21-appendix-e-nursery-result-sheet.md` |
| 3 | `assets/03-nursery-termly-report-page2.png` | Nursery Termly Report Sheet, page 2 | Fourth development domain (Social & Emotional), the Cognitive Domain subject table, comment and signature blocks, next-term fees block, grade key. | `21-appendix-e-nursery-result-sheet.md` |
| 4 | `assets/04-primary-termly-report-page1.png` | Primary Termly Report Sheet, page 1 | Primary header block including attendance, and the Academic Assessment of Cognitive Domain subject table. | `22-appendix-f-primary-result-sheet.md` |
| 5 | `assets/05-primary-termly-report-page2.png` | Primary Termly Report Sheet, page 2 | Affective Domain and Psychomotor blocks side by side, rated E/I/N. Report and signature blocks, next-term fees block, grade key. | `22-appendix-f-primary-result-sheet.md` |
| 6 | `assets/06-pupil-admission-form-page1.png` | Pupil Admission & Information Form, page 1 | Form sections A (School Use Only) and B (Pupil's Information). | `07-module-pupils-guardians.md` |
| 7 | `assets/07-pupil-admission-form-page2.png` | Pupil Admission & Information Form, page 2 | Form sections C (Parent/Guardian), D (Emergency Contact), E (Authorised Persons to Pick Up). | `07-module-pupils-guardians.md` |
| 8 | `assets/08-pupil-admission-form-page3.png` | Pupil Admission & Information Form, page 3 | Form sections F (Health & Safety), G (Other Important Information), H (Document Checklist), I (Parent Declaration), J (School Confirmation). | `07-module-pupils-guardians.md` |

## How to read these images as a builder

Three cautions, because these are scans of paper forms and paper forms encode assumptions that do not survive being turned into software unexamined.

**A ruled line is a field, not a length.** The forms use a full-width dotted or solid rule for every value regardless of whether the value is a surname or a single digit. Field lengths in this specification come from the field tables, not from the width of the line on the page.

**A blank column is not always an input.** On the weekly sheet, `Parent's Comment` is a blank line on paper because the parent writes on the paper. Whether it is an input in the software is an open question, recorded as question 4 in `25-open-conflicts-to-resolve.md`, because parents have no accounts in this system.

**The forms show one term, one pupil.** Every one of them is a single-pupil artefact filled in by hand. The management screens that produce them are grids over a whole arm, and the grid, not the sheet, is what a teacher spends time in. Screen design should follow the entry patterns in the module files; the images fix what the output has to look like when it is printed or shown to a parent.
