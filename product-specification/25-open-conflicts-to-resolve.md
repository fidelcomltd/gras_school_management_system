# Open Conflicts and Decisions Arising from the School's Own Forms

The school's own result sheets and admission form contradict several things written earlier in this specification on the basis of the seed data and the Little Angels sample. This file lists every contradiction, states which way the specification has been provisionally set, and gives the alternatives.

**How the orchestrator should treat this file.** Every item below has a provisional resolution, and the rest of the specification is written to that resolution, so nothing here blocks work starting. What each item does carry is a risk that work is redone if the school answers differently. Items marked **HIGH** touch stored data or computation and are expensive to reverse after marks exist. Items marked **LOW** are template or label changes. Build HIGH items last where sequencing allows, and raise all of them with the school before the first term is scored.

---

## 1. The grading scale is different, and it has a gap. HIGH

The seeded scale in `04-module-school-settings.md` has six bands: A 80-100, B 70-79, C 60-69, D 50-59, E 40-49, F 0-39.

Both of the school's own sheets print eight bands:

| Grade | Range | Abbreviation |
|---|---|---|
| A+ | 90-100 | Very excellent |
| A | 85-89 | Excellent |
| B | 75-84 | Very good |
| B- | 70-74 | Good |
| C+ | 60-69 | Average |
| C | 50-59 | Fair |
| D | 40-49 | More effort |
| E | 20-39 | Not Now |

These are irreconcilable: an 82 is a B on the school's scale and an A on the seeded one. The school's own sheet wins, so the seed data in `04-module-school-settings.md` is replaced by the eight bands above, plus a ninth beneath them covering 0 to 19.

**The gap.** The printed scale covers 20 to 100. Nothing covers 0 to 19. The validation rule in 6.2.6 requires contiguous coverage of 0 to 100 and will reject this scale as printed.

**RESOLVED by the school.** A ninth band is added: **F 0-19**, seeded with the descriptor `Fail`, which is this specification's word rather than the school's and is editable. E stays at 20-39 exactly as the sheet prints it. Nine bands, contiguous, no visible change to the eight rows the school already prints.

The scale is school-wide: nursery and primary print the identical key, so one scale serves both sections.

**Framing correction.** This item was originally written as though the grading scale were a specification decision. It is not. 6.2.5 has always specified grading as editable school data with full CRUD, ten validation rules, versioning and publication snapshotting, and Appendix A entry 13 states the principle. What is recorded above is **seed data**: what the grading screen contains on first run. The school changes any of it in seconds without a developer.

Two things about grading do stay in code, and should:

- **The contiguity rule.** A scale must cover 0 to 100 with no gap and no overlap. The tempting relaxation here was to let the school enter its scale exactly as printed, gap and all. Refused, because a gap produces a blank grade cell in front of a parent and the school discovers it in December.
- **How a grade is derived**, meaning the mapping of a percentage to the band containing it. Configurable thresholds, not a configurable formula. A school-defined expression engine is the natural next request and should be declined: it is an untestable surface nobody at the school can debug, serving flexibility this school will not use.

---

## 2. The assessment structure is different. HIGH

Seeded structure: First CA 15, Second CA 15, Assignment 10, Examination 60.

Both sheets show three columns only: **1st CA, 2nd CA, Exam**. There is no assignment column on either. The nursery sheet labels its columns `Exam 60%` and `Total 100%`, which fixes the examination at 60 and the two continuous assessments at 40 between them.

**RESOLVED by the school, with a durability requirement attached.** The seed is **1st CA 20, 2nd CA 20, Examination 60**, and the assignment component is removed.

The school additionally directed that **reintroducing an assignment must remain a configuration change**, so that this product can be onboarded at another school that uses one. 6.2.13 now carries the requirements that makes real: no surface may assume a component count, the continuous assessment total is computed from `is_examination` rather than from a constant, column headings and PDF widths are derived, the portal returns components as an ordered array rather than as named fields, and the import template generates its component columns at download time. Two named seed profiles, `gras_default` and `with_assignment`, make a second installation's first run fast.

Note that another school means another installation, per Appendix A entry 1. This product is deliberately not multi-tenant, and profiles are a setup convenience rather than a step toward tenancy.

This remains the most expensive item to get wrong. Per 6.2.6 the structure locks for the whole session once the first mark is entered, so it must be right before scoring begins.

---

## 3. The sheets print no position and no class average. MEDIUM

Neither of the school's sheets carries a position in class, a class average, a subject position, or a subject highest and lowest. The Little Angels sample did, and a substantial amount of section 8 and Appendix A entries 21, 30 and 47 is about how to compute and present them fairly.

Provisional resolution: **all of it is still computed and stored, and none of it prints on the parent-facing sheet by default.** Position and class statistics appear on the arm broadsheet in `15-reporting-requirements.md`, where the head teacher needs them, and are available as an optional block the school can switch on.

| Option | Effect |
|---|---|
| A. Compute, store, do not print (**chosen**) | Matches the sheets. Costs nothing later: switching the block on is a template change, not new computation. |
| B. Print position anyway | Contradicts the sheets and invites the cohort-comparison problem Appendix A entry 30 describes. |
| C. Stop computing positions | Rejected. The broadsheet and the merit list in section 10 both need them, and prize-giving needs a ranking. |

---

## 4. The weekly sheet has a Parent's Comment line. HIGH

Every day panel on the weekly report sheet ends with `Parent's Comment`. On paper the sheet goes home in the child's bag and the parent writes in it. In this system parents have no accounts, hold only an anonymous pin, and the portal is read-only.

**RESOLVED by the school.** Option A is confirmed, with two clarifications the school added: the field is **optional**, and it may be entered by **a teacher or an administrator**, not the class teacher alone.

- **Optional means optional.** The field is nullable, never required to save a day panel, never required to publish a weekly sheet, and an empty one prints as an empty line rather than as a placeholder. No validation, no reminder, no completeness percentage counts it. The school expects it to be empty most weeks and that is not a defect.
- **Who may write it.** No new privilege. The field is written under the existing `weekly.enter`, which the seeded roles give to Class Teacher arm-scoped and to Head Teacher school-wide. School Administrator does **not** hold `weekly.enter` in the seed, so if the school wants its administrators writing these comments it is a role edit on the roles screen, not a code change. Every write is attributed and audited like any other, so the sheet can always answer who typed it.
- **Still no parent-facing write.** The portal stays read-only and no anonymous write endpoint is built. Option B remains available as its own module later, and the moderation, length-limit, rate-limit and abuse questions below still have to be answered before it is.

| Option | Effect |
|---|---|
| A. Teacher or administrator transcribes, field optional (**chosen**) | No new attack surface. Costs teacher time, and the field will often be empty. |
| B. Parent submits through the portal inside a viewing session | Genuinely useful, and it makes an unauthenticated anonymous endpoint that writes text into the school's database. Needs length limits, moderation, rate limiting, and an answer to what happens when the comment is abusive. Recommend deferring to a second version. |
| C. Drop the field | Loses a two-way channel the school clearly uses on paper. |
| D. Print a blank line on the PDF only | Middle path: parents write on the printout, nothing is stored. Keeps the paper workflow and stores nothing. |

If the school wants option B, say so and it needs its own module rather than a field on this one.

---

## 5. The sheets carry a next-term fees block, and fees are out of scope. MEDIUM

Both sheets end with a fees table: Tuition Fee, Exam & PTA, Books, Toiletries, Party Fee, Outstanding Fee, Total. Section 3.2 of `00-document-overview.md` puts fees and payments explicitly out of scope, and that exclusion was deliberate.

**RESOLVED by the school: a notice, with entry screens, and no logic attached.** Six configurable labels plus a computed total, amounts set per class level per term, and the outstanding figure alone typed per pupil.

6.2.13 now specifies the entry screens: a grid of labels against class levels for a term with a `copy from previous term` action, and a bulk per-arm grid for the outstanding figure. It also states explicitly what this block does not do, because those are requirements rather than omissions: no invoice, no receipt, no payment record, no part payment, no balance carried between terms, no arrears calculation, no arithmetic beyond the printed total, and **no pupil is ever blocked from a result, a pin or a portal lookup by a fee figure.** That last one should be refused if it is ever raised, since it puts the system between a child and their marks.

Labels are section-scoped, since Toiletries and Party Fee are nursery concerns.

---

## 6. Nursery and primary rate behaviour on different scales. MEDIUM

The nursery sheet rates development indicators on four points, **E Excellent, S Satisfied, I Improving, N Needs Improvement**, and gives every indicator its own comment column. The primary sheet rates affective and psychomotor traits on three points, **E Excellent, I Improving, N Needs Improvement**, with no comment column. Neither is the five-point numeric scale adopted from the Little Angels sample in Appendix A entry 48.

Provisional resolution: **the rating scale becomes a property of the rating block rather than a single school-wide setting.** Three seeded scales: nursery development four-point, primary trait three-point, and the five-point numeric scale retained as an unused alternative. This replaces the single `trait_scale` table in 6.2.7 with a `rating_scale` table and a scale reference on each block.

The alternative is one scale forced on both sections, which would mean reprinting one of the two sheets. Not recommended.

Both scales are school data, like the grading bands. A school may add scale points, rename them, change the printed legend, or attach a different scale to a new domain, all through 6.2.13. What stays in code is that a rating is a choice from a fixed set of points rather than a free value, since a rated block whose points vary per pupil cannot be printed as a grid.

---

## 7. The nursery sheet has no attendance, the primary sheet does. LOW

The primary header carries Time School Opens, Time Present and Time Absent. The nursery header carries none of the three. The wording is the school's own and is kept on the printed sheet even though the values are counts of days rather than times.

Provisional resolution: attendance is captured for both sections and printed only where the sheet has a place for it. A nursery teacher still enters it, because the reports in section 10 use it and because the school may add it to the nursery sheet later.

---

## 8. Pupil status gains a Pending value. LOW

The admission form's section A offers **Active, Pending, Withdrawn**. The existing pupil status enum is active, transferred, withdrawn, graduated. Pending is new and means an admission in progress: a record created, not yet approved, not yet countable in an arm.

Provisional resolution: status becomes pending, active, transferred, withdrawn, graduated, with pending as the state a record is created in and `active` set at admission approval. A pending pupil is excluded from every roster, every enrolment count, every score sheet and every capacity check. Detail is in `07-module-pupils-guardians.md`.

---

## 9. Two fields on the admission form are collected but never shown to parents. HIGH for compliance, not for build

Section E of the admission form asks who is **not** allowed to collect the child, and section F asks about allergies, medical conditions and regular medication. Both are necessary for the school to care for a child safely. Both are also the most sensitive data in the system, and the barred-persons field in particular may record a family court situation.

Provisional resolution: collected, stored, access restricted to a named privilege, and **excluded from the portal, the result sheet, the weekly report, the verification page and every export except a single audited safeguarding report**. This follows the existing rule in section 9.9 and Appendix A entry 43 for health data and extends it. The school must have parental consent for the health fields, which the declaration in section I of the form provides in writing.

---

## Current status

| Item | Status |
|---|---|
| 1. Grading scale | **Resolved.** Nine bands, F 0-19 added, seeded and editable by the school. |
| 2. Assessment structure | **Resolved.** 20/20/60 seeded, with a durability requirement so an assignment can be reintroduced by configuration. |
| 3. No position or class average on the sheets | Provisional, low risk. Computed and stored, printed only on the broadsheet. Reversible as a template change. |
| 4. Parent's Comment on the weekly sheet | **Resolved.** Optional free-text field, written by a teacher or an administrator under the existing `weekly.enter` privilege. Portal stays read-only; no parent-facing write endpoint. |
| 5. Next-term fees block | **Resolved.** A notice with entry screens and no logic attached. |
| 6. Two rating scales | Provisional. Scale-per-block, three seeded scales, all editable. |
| 7. Nursery has no attendance line | Provisional, low risk. Captured for both sections, printed only for primary. See Appendix B question 20. |
| 8. Pending pupil status | Provisional, low risk. Specified in 6.5.14. |
| 9. Health and barred-persons data | Provisional on compliance, settled for build. Collected, restricted, audited, present in exactly one export. |

Still outstanding, in order of cost of getting it wrong:

1. The smaller confirmations in `17-appendix-b-open-questions.md` part 2, questions 19 to 27. Question 19's indicator lists are now confirmed by the school; the rest remain.

**No item in this file now blocks build.** Items 1, 2, 4 and 5 are settled by the school. Items 3, 6, 7, 8 and 9 remain provisional, all of them reversible as template or seed changes, and all are recorded so that a different answer later is a known cost rather than a surprise.

## A note on configurability, since it recurs

Three of the nine items above looked like specification decisions and were really seed-data decisions. The distinction that resolves them, and which should be applied to anything similar that comes up later:

**Lists and thresholds are school data. Algorithms and invariants are code.**

Configurable, with no developer involved: grade bands and their boundaries and descriptors, assessment components and their maximums, class levels and the progression chain, subjects and their mapping, rating scales and their points and legends, development domains and indicators, affective and psychomotor traits, fee labels and amounts, document checklist types, the registration number format, and the trait and indicator comment settings.

Fixed in code, deliberately: the requirement that a grading scale covers 0 to 100 with no gap; the requirement that assessment components sum to 100; rounding and tie-breaking rules; how a position is computed and against which cohort; the session lock on assessment structure; and the rule that a rating is a choice from a fixed point set rather than a free value.

The line is drawn where a school-side change could produce two documents that disagree with each other, or a number a parent can dispute and nobody can reconstruct.
