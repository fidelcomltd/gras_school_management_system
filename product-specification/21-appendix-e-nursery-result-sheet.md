# Appendix E. Nursery Termly Report Sheet Contract

**Design reference:** `assets/02-nursery-termly-report-page1.png` (page 1) and `assets/03-nursery-termly-report-page2.png` (page 2).

This appendix supersedes `18-appendix-c-result-sheet-contract.md` for pupils in the **Nursery** section. Appendix C was written against another school's online sheet before GRAS supplied its own forms; it is retained for its general rules on sourcing and rendering, and its field-level layout no longer governs.

A nursery report is **two A4 portrait pages** and carries no position, no class average and no attendance block. Two rating systems appear on it: a four-point developmental rating across four domains, and a conventional scored subject table.

## E.1 Section routing

The system decides which sheet to render from the pupil's class level, not from a setting on the pupil.

- `class_level.section` is `nursery` or `primary`. This value already exists in 6.4 and now carries rendering weight.
- Nursery levels (Nursery 1, Nursery 2, Nursery 3) render this appendix. Primary levels render `22-appendix-f-primary-result-sheet.md`.
- A pupil's section for a term is the section of the level they were enrolled in at the term's end date, so a pupil promoted from Nursery 3 to Primary 1 gets nursery sheets for the nursery year and primary sheets thereafter.
- Both sections share one grading scale, one assessment structure, one session and term structure, and one weekly report format. What differs is the rating blocks, the subject lists, and the header.

## E.2 Header block

The nursery header is a five-line label-value list, two columns, matching the paper form's order exactly.

| Field | Source | Format and example |
|---|---|---|
| document_title | Computed | Fixed string `NURSERY TERMLY REPORT SHEET`. |
| pupil_full_name | Stored | Printed on the `Name` line. Surname in capitals then given names. |
| academic_year | Stored | Printed on the `Year` line. The session's calendar span, `2026/2027`. Note this line is labelled Year on the nursery form and Session on the primary form; both carry the same value. |
| pupil_age | Computed | Whole years at the term end date, per 6.5.3. `4`. |
| term_name | Stored | `First Term`. |
| pupil_class | Computed | Level name plus arm label by the rule in 6.4.5. `Nursery 2A`. |
| session_name | Stored | Printed on the `Session` line. Same value as academic_year; the form carries both labels and the school prints the same string in each. |
| instructor_name | Stored | The arm's form teacher, captured at publication so a staff change later does not rewrite an issued sheet. Printed on the `Instructor` line. |
| next_term_begins | Stored | DD/MM/YYYY from the following term's start date. |
| overall_grade | Computed | The band letter for the term average, resolved against the configured grading scale. `A`. |
| term_average | Computed | Two decimal places. `78.50`. |
| rating_key_line | Snapshot | The four-point legend printed as one line beneath the header: `E-Excellent  S-Satisfied  I-Improving  N-Needs Improvement`. |
| revision_notice | Computed, conditional | As Appendix C.1. Present only where revision_number exceeds 1. |

The nursery header has **no attendance fields**, per `25-open-conflicts-to-resolve.md` item 7. Attendance is still captured for nursery arms and used in reporting; it simply does not print here.

## E.3 Development domain blocks

Four blocks, in this order, under the heading `Assessment of Development Domain`. Each block is a table: indicator name down the side, four rating columns headed E, S, I, N, and a Comments column.

| Field | Source | Format |
|---|---|---|
| domain_name | Snapshot | The block heading, printed in bold capitals. |
| indicator_name | Snapshot | One row per configured indicator, in configured display order. |
| indicator_rating | Stored | One of E, S, I, N, rendered as a mark in the matching column. An unrated indicator leaves all four columns blank and the row still prints. |
| indicator_comment | Stored, optional | Free text up to 120 characters in the row's Comments cell. Per-indicator comments are unique to the nursery sheet; the primary trait blocks have no comment column. |

### Seeded indicators

Transcribed from the supplied scan and **confirmed correct by the school**, including `Home work on High Quality` in domain 3, which had looked like a possible scanning artefact.

This is the seed, not the ceiling. **The school adds its own indicators, and whole new domains, through the configuration screen in 6.2.13** without any code change. Domains carry their own rating scale and their own choice of whether a comment column appears, so a fifth domain rated on three points is a configuration action.

**Domain 1: Maths Readiness** (4 indicators)
Ability to Count · Write Number Clearly · Ability to Recognize numbers · Ability to Reason and Answer question

**Domain 2: Language/Communication Development** (13 indicators)
Ability to recite letters · Ability to Recognize Upper/Lower case · Can write Upper/Lower case · Can construct simple sentence · Can identify object · Can recognize similarities & differences · Know Letters and Alphabet in sequence · Can Trace Letters & Object · State own name and write · Expression of own feeling and thought · Listen attentively and contribute to discussion · Shows interest in books · Speaks clearly · Able to speak with right vocabulary

**Domain 3: Personal & Physical Development** (16 indicators)
Can Recognize different Colours · Can recognize different shapes · Hold Pencils Correctly & firmly · Take simply instruction/directions · Work independently · Can run and jump well · Can Catch, Bounce, and throw ball · Move all parts of the body very well · Fit small items together · Logical reasoning · Cleanliness · Persistence · Wear cloth independently · Potty trained · Home work on High Quality

**Domain 4: Social & Emotional** (12 indicators)
Happy at School · Behaves Well in School · Etiquette and manners · Expression and Emotions and Feeling · Accept Correction · Honesty · Obedient to Instruction · Behaves well in Class · Work and Mixes well with Others · Concentration · Attendance to Class · Punctuality

Domains 1 to 3 print on page 1. Domain 4 opens page 2.

Indicators are configurable records, not values in code, following the same rule as class levels in Appendix A entry 13. A domain or indicator that has ever been rated is archived rather than deleted, per 9.4. Archived indicators stay on historical sheets and leave new entry screens.

## E.4 Cognitive Domain subject table

A conventional scored table under the heading `COGNITIVE DOMAIN`, identical in structure to the primary sheet's table.

| Column | Source | Format |
|---|---|---|
| Subject | Snapshot | Subject name in configured order. |
| One column per assessment component | Stored | Generated by iterating the active components in display order, per 6.2.13. With the GRAS default this produces `1st CA` out of 20, `2nd CA` out of 20 and `Exam 60%` out of 60. The heading is composed from each component's own name and maximum, so a school that adds an Assignment gets a fourth column with no change to this contract. The examination component renders `ABS` where the exam_absent flag is set. Blank where unentered. |
| Total 100% | Computed | Integer out of 100. Equals the continuous assessment total, meaning the sum of every non-examination component, where the pupil was absent from the examination. |
| Grade | Computed against snapshot | Band letter from the grading scale. |
| Comment | Snapshot | The band's abbreviation word: Very excellent, Excellent, Very good, Good, Average, Fair, More effort, Not Now, Fail. |

**The column count is not fixed.** The renderer must lay out two to six component columns without overflowing A4 portrait, computing widths from the count rather than using fixed values. The nursery form as printed shows three; that is the current configuration, not a constraint on the layout.

**Grand Total row.** The last row of the table sums the 1st CA, 2nd CA, Exam and Total columns down the subject list. It carries no grade and no comment.

### Seeded nursery subjects

Number work · Letter work · Phonics · Quantitative reasoning · Verbal reasoning · Literature · Pre Science · Social habit · Health habit · Handwriting · Christian Religious Knowledge · Computer Science · Creative skills · Rhyme

Fourteen subjects. These are mapped to nursery levels through the ordinary subject mapping in 6.6, per session and term, so the school can vary them by nursery year without any change here.

## E.5 Comment and signature block

| Field | Source | Format |
|---|---|---|
| teacher_comment | Stored | Free text up to 300 characters, on the `Teacher's comment` line. |
| teacher_signature_area | Layout | A `Sign & Date` area to the right, printed as a ruled space. Signed by hand. |
| head_teacher_comment | Stored | Free text up to 300 characters. Required before publication, per 6.7.9. |
| head_teacher_signature | Snapshot, optional | The uploaded signature image where present, otherwise a ruled line, with a date area beside it. |

Both comment lines print whether or not a signature image exists, because the school signs these by hand at the point of handing them over.

## E.6 Next-term fees block

A display-only notice, per `25-open-conflicts-to-resolve.md` item 5. Two columns, a label and an amount, with a computed total.

| Field | Source | Format |
|---|---|---|
| fee_line_label | Snapshot | Configurable labels, seeded: Tuition Fee, Exam & PTA, Books, Toiletries, Party Fee, Outstanding Fee. |
| fee_line_amount | Stored | Naira amount with thousands separators and no kobo, `45,000`. Amounts for the first five lines are set per class level per term by an administrator. |
| outstanding_fee_amount | Stored, per pupil | The only per-pupil figure in the block. Typed by an administrator or bursar from the school's own records. Blank prints as a dash rather than as zero. |
| fee_total | Computed | The sum of the printed lines. |

Nothing here is invoiced, receipted, reconciled or carried forward, and no pupil is ever blocked from a result by a figure in this block. It is a printed notice of what to pay next term. The entry screens and the full list of things this block deliberately does not do are in 6.2.13.

## E.7 Grade key block

The grading key printed on page 2, read from the snapshot, so a sheet reprinted years later shows the boundaries it was graded against rather than whatever the school uses by then.

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
| F | 0-19 | Fail |

Nine rows. The school's printed key has eight and stops at 20; `F 0-19 Fail` is added beneath to satisfy the contiguity rule in 6.2.5, and its descriptor is editable at the grading screen. The whole table is read from the snapshot, so a sheet reprinted years later shows the boundaries it was graded against, including any the school has since changed.

## E.8 Computation notes specific to nursery

- `term_average` is the grand total of the Total column divided by the number of subjects offered, to two decimal places. It is not weighted by domain ratings: the development domains contribute nothing to any number.
- The grade for a subject and for the term average is resolved against whatever bands the school has configured at publication, not against the nine seeded here. A school that collapses the scale to five bands next session gets five-band grades on next session's sheets and keeps nine-band grades on this session's, because of the snapshot.
- Development ratings are never converted to marks, never averaged and never ranked. Any request to do so should be refused and raised, because a four-point pastoral judgement about a three-year-old is not an input to arithmetic.
- The completeness gate for a nursery result set requires every subject cell, every development indicator rating, the teacher's comment and the head teacher's comment. Indicator comments are optional; indicator ratings are not.
- Everything else, meaning positions, class averages, subject statistics and the annual cumulative result, is computed exactly as specified in `13-result-computation-rules.md` and simply does not print here.
