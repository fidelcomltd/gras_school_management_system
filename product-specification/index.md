# GRAS School Management System: Specification Index

Product specification for the Golden Royal Ark School Management System, split into one markdown file per buildable unit for an AI orchestrator generating build tasks.

**Revision 3.1.** Design reference images live in `assets/` and are catalogued in `24-design-reference-images.md`.

Revision 3.1 records four decisions from the school: a ninth grading band `F 0-19`, the 20/20/60 assessment seed with a requirement that an assignment component stay reintroducible by configuration, the fee block confirmed as a notice with entry screens and no logic, and the transcribed development indicator lists confirmed with the school free to extend them. All four are in `04-module-school-settings.md` 6.2.13, with status in `25-open-conflicts-to-resolve.md`.

> **This artifact is now ahead of the Word document.** `GRAS_School_Management_System_Specification.docx` reflects revision 1 and does not yet contain the changes in revision 3 below. Where the two disagree, **these markdown files are authoritative.** The docx will be brought back into line on request.

---

## What changed in revision 3

The school supplied eight images: its own nursery result sheet, its own primary result sheet, its weekly report sheet, and its three-page pupil admission form. These are the school's own documents rather than another school's sample, so they override earlier assumptions.

1. **Result sheets are now section-specific.** Nursery and primary get different sheets with different rated blocks, different subject lists and different headers. Contracts in `21-appendix-e-nursery-result-sheet.md` and `22-appendix-f-primary-result-sheet.md`. `18-appendix-c-result-sheet-contract.md` is superseded for layout and says so in its new C.10.
2. **A weekly report feature is new**, for both sections: one sheet per pupil per week, five weekday panels, eight pastoral lines each. Parents read it on the portal, class teachers, administrators and head teachers write it. Module in `20-module-weekly-reports.md`, render contract in `23-appendix-g-weekly-report-contract.md`, portal behaviour in `11-module-parent-portal.md` 6.9.10.
3. **Pupil management is rewritten** around the school's admission form. A pupil is now created `pending` and becomes active only on approval, through a nine-step resumable flow. New entities for contacts across five roles, health and safety, authorised and barred collection persons, and an admission document checklist with file upload. See `07-module-pupils-guardians.md`.
4. **Nine conflicts** between the school's forms and what was specified earlier are catalogued with options in `25-open-conflicts-to-resolve.md`. Two of them, the grading scale and the assessment structure, change stored data and computation and are the most expensive to get wrong.

### Revision 2, still in force

**Access pins are unbound.** Any valid pin with any registration number opens that pupil's results, spending one use. A school directive against the original recommendation. See `10-module-access-pins.md`, `16-appendix-a-decisions.md` entry 35, and `25-open-conflicts-to-resolve.md`.

---

## Read these first

| Order | File | Why |
|---|---|---|
| 1 | `25-open-conflicts-to-resolve.md` | Nine decisions. Items 1, 2 and 5 are now resolved by the school; item 4, the weekly sheet's Parent's Comment line, is the only one still open that changes what gets built. Closes with the configurability principle that governs anything similar arising later. |
| 2 | `24-design-reference-images.md` | Catalogue of the eight supplied images and which spec file governs each. Any agent building a screen or a PDF should open the image. |
| 3 | `00-document-overview.md` | Purpose, readers, product scope in and out. |
| 4 | `01-actors-and-privileges.md` | Roles and the full privilege register. Assumed by every module. |
| 5 | `02-data-model.md` | Entity inventory and relationships, including the revision 3 amendments in its section 5.5. |

## Suggested build order

1. `04-module-school-settings.md` first among the modules. Nearly everything reads configuration from it, and its new 6.2.13 amendment changes the grading scale, the assessment structure and the rating scales. Building against the old seed data means rework.
2. `03-module-admin-roles-audit.md` and `05-module-sessions-terms.md` in parallel once settings exists. Sessions and terms matter earlier than before, because weekly report weeks are derived from term dates.
3. `06-module-class-levels-arms.md`. The `section` field on a class level now drives result sheet routing, so it is load-bearing rather than descriptive.
4. `07-module-pupils-guardians.md`. Substantially larger than in revision 1. The nine-step admission flow in 6.5.11 and the completeness model in 6.5.12 are the parts to read closely, and the pending status in 6.5.14 affects almost every query that touches pupils.
5. `08-module-subjects.md`. Seeded subject lists now differ by section: 14 nursery subjects, 19 primary.

**A standing constraint for every task touching marks or sheets:** the assessment component list is configuration, and no surface may assume how many components there are. Column sets, continuous-assessment totals, PDF widths, portal payload shape and import templates are all derived from the component list at runtime. The requirements are in `04-module-school-settings.md` 6.2.13 and they exist so this product can be onboarded at a school that uses an assignment component.
6. `09-module-results.md`, the largest module, worth splitting into several tasks. Its new 6.7.12 covers section-specific sheets and what changes in non-academic input between the two sections.
7. `20-module-weekly-reports.md` can be built in parallel with results. It shares the term and arm structure but touches no marks, no computation and no result state machine.
8. `10-module-access-pins.md` and `11-module-parent-portal.md` once publication is stable. The portal now serves results and weekly reports from one viewing session.
9. `13-result-computation-rules.md` is not a build task. It is the specification the computation engine must satisfy, and its worked example is a regression fixture. Note its numbers assume the old assessment structure and grading scale and need restating once item 2 in the conflicts file is settled.
10. `12-end-to-end-flows.md` is integration-test material once the modules it walks exist.
11. `14-non-functional-requirements.md` and `15-reporting-requirements.md` cut across everything and should inform tasks from the start, particularly the entry-resilience requirement in 9.8.2, which now applies to weekly report entry as much as to score entry.

## File index

| # | File | Covers | Notes |
|---|---|---|---|
| 1 | `00-document-overview.md` | Document overview | Purpose, readers, product overview, scope. |
| 2 | `01-actors-and-privileges.md` | Actors and privilege matrix | Privilege register. Revision 3 adds admission, safeguarding, document and weekly privileges, and replaces `guardian.*` with `contact.*`. |
| 3 | `02-data-model.md` | Data model | Entity inventory and relationships. Section 5.5 lists the four structural changes in revision 3. |
| 4 | `03-module-admin-roles-audit.md` | Module 6.1 | Admin accounts, roles, escalation prevention, audit log. |
| 5 | `04-module-school-settings.md` | Module 6.2 | Configuration. **New 6.2.13** replaces the grading scale, assessment structure, rating scales and trait lists, and adds development domains and the fee notice. |
| 6 | `05-module-sessions-terms.md` | Module 6.3 | Sessions, terms, promotion. Term dates now also derive weekly report weeks. |
| 7 | `06-module-class-levels-arms.md` | Module 6.4 | Progression chain, arms, display-name rule. `section` drives sheet routing. |
| 8 | `07-module-pupils-guardians.md` | Module 6.5 | **Rewritten.** Admission form sections A to J, nine-step flow, five contact roles, health and safeguarding, document checklist, pending status. |
| 9 | `08-module-subjects.md` | Module 6.6 | Subject to level mapping and per-arm exceptions. |
| 10 | `09-module-results.md` | Module 6.7 | Score entry, computation, approval, publication, annual result. **New 6.7.12** covers section-specific sheets. |
| 11 | `10-module-access-pins.md` | Module 6.8 | Unbound pins, batches, uses, the spread control. |
| 12 | `11-module-parent-portal.md` | Module 6.9 | Public portal. **New 6.9.10** adds weekly reports and section-based sheet routing. |
| 13 | `20-module-weekly-reports.md` | Module 6.10 | **New.** Weekly pastoral reports: entities, entry grid, publication, parent view, endpoints. |
| 14 | `12-end-to-end-flows.md` | End-to-end flows | Five multi-module walkthroughs. Predates revision 3; the pin flow is current, the registration flow is superseded by 6.5.11. |
| 15 | `13-result-computation-rules.md` | Computation rules | Algorithm and worked example. Numbers need restating after conflicts item 2. |
| 16 | `14-non-functional-requirements.md` | Non-functional requirements | Auth, authorisation, audit, deletion rules, uploads, performance, backup, NDPA 2023, printing. |
| 17 | `15-reporting-requirements.md` | Reporting | Thirteen reports, plus **eight more in 10.2** including the class safeguarding sheet. |
| 18 | `16-appendix-a-decisions.md` | Appendix A: decisions | Numbered decisions with rationale. Entries 46 and 48 are partly superseded; see conflicts items 3 and 6. |
| 19 | `17-appendix-b-open-questions.md` | Appendix B: open questions | Questions 1 to 18, plus **part 2, questions 19 to 27** from the new forms. |
| 20 | `18-appendix-c-result-sheet-contract.md` | Appendix C: original sheet contract | **Superseded for layout.** See its C.10. Still authoritative for the source model and renderer rules. |
| 21 | `19-appendix-d-glossary.md` | Appendix D: glossary | Nigerian education and domain terms. |
| 22 | `21-appendix-e-nursery-result-sheet.md` | Appendix E: nursery sheet | **New.** Section routing, header, four development domains with 45 indicators, cognitive table, fees, grade key. |
| 23 | `22-appendix-f-primary-result-sheet.md` | Appendix F: primary sheet | **New.** Header with attendance, 19-subject table, affective and psychomotor blocks, fees, grade key, and what the sheet deliberately omits. |
| 24 | `23-appendix-g-weekly-report-contract.md` | Appendix G: weekly sheet | **New.** Page, header, five day panels, renderer rules. |
| 25 | `24-design-reference-images.md` | Design reference images | **New.** Catalogue of the eight images in `assets/`, with cautions on reading paper forms as software. |
| 26 | `25-open-conflicts-to-resolve.md` | Open conflicts | **New.** Nine conflicts with provisional resolutions and alternatives. |

## Design reference images

In `assets/`, catalogued in `24-design-reference-images.md`:

`01-weekly-report-sheet.png` · `02-nursery-termly-report-page1.png` · `03-nursery-termly-report-page2.png` · `04-primary-termly-report-page1.png` · `05-primary-termly-report-page2.png` · `06-pupil-admission-form-page1.png` · `07-pupil-admission-form-page2.png` · `08-pupil-admission-form-page3.png`

## Cross-cutting rules that apply throughout

- Every list, search and filter follows the pagination and search conventions in `14-non-functional-requirements.md` 9.5.
- Every delete-or-deactivate decision follows the single rule and entity table in `14-non-functional-requirements.md` 9.4.
- Every privilege check is server-side middleware per `14-non-functional-requirements.md` 9.2 and the register in `01-actors-and-privileges.md`. Hiding a menu item is not enforcement.
- **Every query touching pupils must exclude `pending` status** unless it is the admissions queue. This is the most easily missed consequence of revision 3.
- Health data, barred-persons data and the section H documents are gated by `pupil.safeguarding.view`, audited on read, and appear in exactly one export: the class safeguarding sheet. They never reach the portal, a result sheet or a weekly report.
- Design decisions have numbered entries in `16-appendix-a-decisions.md`. Cite an entry rather than re-deriving a rule.
- Unresolved questions live in `17-appendix-b-open-questions.md` and `25-open-conflicts-to-resolve.md`. A task should not block on one unless it is marked blocking.
