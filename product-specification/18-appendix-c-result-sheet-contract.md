# Appendix C. Result Sheet Data Contract

The school has still not given me its own result sheet, but it has given me a screenshot of another Nigerian school's online result sheet, Little Angels High School in Owerri, as a reference for shape. What follows is the list of every value the system can put on a sheet, where each value comes from, and how it is formatted, arranged to match the layout of that reference wherever the reference and the seed data agree. Where the reference contradicts GRAS's own seed data, GRAS's data wins, and the disagreement is noted. C.9 sets out exactly what was taken from the reference, what was left out, and why. When the school eventually sends its own sheet, the work is to rearrange these fields, not to invent new ones. If the school's sheet needs a value that is not in this list, that is a gap in this appendix and in the data model, and it should be raised rather than calculated by hand at the printer.

**Source** in the tables below takes one of three values. **Stored** means the value is a column read straight from the database. **Computed** means the system derives it at computation time and stores the result, per section 8. **Snapshot** means it comes from the configuration copy frozen into the result set at publication, per 6.2.9, so a published sheet reprinted in three years is identical to the original.

Fields marked optional are switched on and off in settings and every renderer must cope with their absence without leaving a hole in the layout. Blood group, genotype and the medical note are deliberately absent from this contract and no setting adds them, per Appendix A entry 43.

## C.1 Header block

| Field | Source | Format and example |
| --- | --- | --- |
| school_name | Snapshot | Text, up to 120 characters, printed as entered. Golden Royal Ark School. |
| school_address | Snapshot | Text, up to 200 characters, may wrap to two lines. |
| school_telephone | Snapshot | Text as entered, so that a leading zero survives. 0803 000 0000. |
| school_email | Snapshot, optional | Text. Omitted from the block if blank rather than printing an empty line. |
| school_motto | Snapshot, optional | Text, up to 100 characters. |
| school_logo | Snapshot, optional | Raster image, rendered at a fixed height with the aspect ratio preserved. A missing logo leaves the space blank and does not shift the header. |
| document_title | Computed | One of two strings: TERMINAL REPORT SHEET for a term sheet, ANNUAL REPORT SHEET for a cumulative one. |
| session_name | Stored | Text in the school's own form. 2026/2027. |
| term_name | Stored | Text. First Term. Absent from the annual sheet. |
| pupil_full_name | Stored | Surname in capitals then given names, per the school's convention: OKAFOR Adaeze Chioma. |
| registration_number | Stored | Text exactly as issued, never reformatted. GRAS/2024/0087. |
| pupil_class | Computed | The level name and the arm label composed by the single display rule in 6.4.5, so Primary 3A or Primary 3 Gold. Never the level alone and never the arm label alone, per Appendix A entry 12. |
| pupil_sex | Stored | Male or Female, spelled out. |
| date_of_birth | Stored, optional | DD/MM/YYYY. 14/03/2018. |
| pupil_age | Computed | Whole years at the term end date, per 6.5.3, printed as a number followed by the word years. 8 years. |
| admission_date | Stored, optional | DD/MM/YYYY. |
| pupil_photograph | Stored, optional | The 400 by 400 derivative. Excluded from the parent's downloaded sheet by default, per Appendix A entry 39. A missing photograph prints an outlined box, not a broken image. |
| position_in_class | Computed | Ordinal with the cohort, in words the parent can read: 3rd of 28 in Primary 3A. Suffixed with (tied) where the position is shared and the setting is on. |
| position_in_level | Computed, optional | Printed only where the level has more than one arm. Ordinal, cohort name and cohort size: 5th of 54 in Primary 3. Computed on the average, per Appendix A entry 30. |
| next_term_begins | Stored, optional | DD/MM/YYYY, taken from the following term's start date. Omitted on the sheet for the third term unless the school has created the next session. |
| revision_notice | Computed, conditional | Present only where revision_number is above 1. Fixed wording: This result was revised on DD/MM/YYYY. It replaces the sheet issued on DD/MM/YYYY. Printed in the header, not the footer, per 6.7. |
| days_school_opened, days_present, days_absent | Stored, Computed | As defined in C.5. Printed here as well, because the reference layout places attendance in the header band rather than as a separate block. The values are the same fields, shown in two places in this appendix only because their position on the page is what the reference fixes, not because there are two sources for them. |

The reference sheet does not lay these fields out block by block. It groups them into one dense band beneath the school's letterhead: the pupil's name centred on its own line, then two columns of short label-value pairs running down the page in parallel, with the photograph at top right. The left column runs registration number, class, sex, term, session and age in that order. The right column runs total school days, times present, times absent, position in class, the pupil's average and the class average, then the next term's resumption date. That is the grouping this specification adopts for the default layout. It is a page layout decision, not a data decision: every field printed there is still exactly the field defined above, sourced the same way, and Appendix C is not extended just because two fields now sit side by side on the page.

Two things in the reference are not carried over. Its position field read Position 7th 7A Out Of 0, printing a cohort size of zero and mixing the arm label into the ordinal, and its age field read 2025 Yr(s), printing the session year in place of a computed age. Both are defects in that school's software, not features. GRAS's position_in_class and pupil_age fields are computed as specified above and are not weakened to match what the reference happened to display.

## C.2 Subject rows

One row per subject mapped to the pupil's arm for the term, in the order set in 6.6.4. A subject with no marks entered still produces a row, with empty score cells, so that the sheet shows what was offered rather than only what was scored.

| Field | Source | Format and example |
| --- | --- | --- |
| subject_name | Snapshot | Text as configured. Cultural and Creative Arts. |
| subject_code | Snapshot, optional | Short text. CCA. |
| component_score | Stored | One column per assessment component, in the configured order, with the component name and its maximum in the column heading: First C.A. (15). Integer, or blank where no mark has been entered. |
| ca_total | Computed | Integer, the sum of every component that is not the examination. Column heading carries the total available: C.A. Total (40). |
| exam_score | Stored | Integer, or the literal ABS where the exam_absent flag is set, per Appendix A entry 26. Never a zero standing in for an absence. |
| subject_total | Computed | Integer out of 100. Where the pupil was absent from the examination this equals the continuous assessment total, so an ABS row still shows a total. |
| subject_grade | Computed against snapshot | The single letter from the frozen grading bands. A. Printed on the pupil sheet in its own column, per the reference. |
| grade_descriptor | Snapshot, optional | The word attached to the band. Excellent. On the reference this word is what fills the column labelled Comment, alongside the letter grade in its own column. GRAS follows the same two-column pattern: a Grade column and a Comment column carrying subject_grade and grade_descriptor respectively. |
| class_average | Computed | One decimal place. 68.4. This is the only class-wide figure the reference prints per subject, and it is the default for the pupil-facing sheet. |
| subject_position | Computed, optional | Ordinal within the arm for that subject. 3rd. Suffixed with (tied) where shared. **Not printed on the pupil-facing sheet by default**, per the reference, which shows no per-subject position. It remains available to a school that wants it, and it is always present on the arm broadsheet in section 10, where a teacher needs to compare pupils rather than read about one. |
| class_highest | Computed, optional | Integer. 91. Broadsheet-level detail, omitted from the pupil-facing sheet by default for the same reason as subject_position. |
| class_lowest | Computed, optional | Integer. 44. Broadsheet-level detail, omitted from the pupil-facing sheet by default for the same reason as subject_position. |
| number_counted | Computed, optional | Integer. The number of pupils whose marks went into class_average, class_highest and class_lowest, lower than the class size where a pupil was absent from the examination. Kept off the pupil sheet by default along with the two figures it qualifies; shown on the broadsheet, where it prevents a class average and a class size from appearing to contradict each other. |
| subject_teacher_remark | Stored, optional | Text up to 60 characters. Off by default, because it multiplies the teacher's typing by the number of subjects and the reference does not carry one. |
| term_total_previous | Computed, Third Term sheet once annual computation has run, and annual sheet | One column per term already sat, each an integer out of 100, so the sheet shows the shape of the pupil's year subject by subject. Per Appendix A entry 46, this appears beneath the Third Term subject table once the year's figures exist, not only on a separate annual document. |
| cumulative_subject_average | Computed, Third Term sheet once annual computation has run, and annual sheet | Two decimal places. The average of the subject totals across the terms sat. 85.33. |

The default pupil-facing subject row is therefore: subject name, one column per assessment component, the continuous assessment total, the examination score, the subject total, the letter grade, the comment, and the class average. Subject position, class highest and class lowest are built and stored regardless, because the broadsheet in section 10 needs them, but the default renderer leaves them off the pupil's copy. A school that wants them on the pupil sheet too can switch them on; nothing in the data model has to change to do it.

## C.3 Summary block

| Field | Source | Format and example |
| --- | --- | --- |
| subjects_offered | Computed | Integer. 4. |
| total_obtainable | Computed | Integer, the subject count multiplied by 100. 400. |
| total_obtained | Computed | Integer, the sum of the subject totals. 320. |
| term_average | Computed | Two decimal places, always with the trailing zeros shown. 80.00. |
| overall_grade | Computed against snapshot | The letter for the term average. A. |
| overall_descriptor | Snapshot, optional | Excellent. |
| arm_average | Computed | One decimal place. The average of the arm's term averages, so the parent can place their child against the class. |
| subjects_passed and subjects_failed | Computed, optional | Two integers, using the lowest passing band in the snapshot as the boundary. |
| first_term_total, second_term_total, third_term_total | Computed, Third Term sheet once annual computation has run, and annual sheet | Integer totals obtained, one per term sat, shown side by side. Matches the Cumulative Results panel on the reference sheet, which lists the three term totals before the grand total. |
| annual_grand_total | Computed, Third Term sheet once annual computation has run, and annual sheet | Integer, the sum of the term totals actually sat. |
| annual_average | Computed, Third Term sheet once annual computation has run, and annual sheet | Two decimal places. 80.25. |
| annual_grade | Computed, Third Term sheet once annual computation has run, and annual sheet | Letter from the snapshot bands. |
| terms_counted | Computed, Third Term sheet once annual computation has run, and annual sheet | Fixed wording where the count is below three: based on 2 of 3 terms, per Appendix A entry 33. Omitted where all three were sat. |
| annual_position | Computed, Third Term sheet once annual computation has run, and annual sheet | Ordinal with cohort. 5th of 54 in Primary 3. |
| promotion_status | Stored, Third Term sheet once annual computation has run, and annual sheet | One of Promoted, To Repeat, or Promoted on Trial, printed only after the promotion batch is committed. The reference prints this as its own labelled line in a colour that signals the outcome; GRAS prints the same three words without treating colour as load-bearing, per 9.10. |
| core_subject_status | Computed, Third Term sheet once annual computation has run, and annual sheet | One line per core subject named under Appendix B question 4, in the form English Language (74.50%) - Passed or Mathematics (38.20%) - Not passed, using each subject's cumulative_subject_average against the pass mark from Appendix B question 4. This is the field behind the reference's English Language (74.5%) -Passed! and Mathematics (71%) -Passed! lines. |
| next_class | Computed, Third Term sheet once annual computation has run, and annual sheet | The level name only, without an arm label, because arm placement for the coming session is usually not settled when sheets are issued. Primary 4. |

On a standalone annual sheet every field above is always present. On the Third Term terminal sheet the same fields appear, in the same format, beneath the ordinary summary block, but only once the annual computation in 8 and 6.7.10 has been run for the pupil's arm; before that the Third Term sheet simply ends at term_average and overall_grade. This is the one place in the contract where a field's presence depends on a computation having run rather than on a setting, and it is recorded as a decision in Appendix A entry 46.

## C.4 Affective and psychomotor blocks

Two side panels of the same shape, from the trait lists in 6.7.6, labelled Skills for the psychomotor block and Behaviour for the affective block, matching the reference sheet's own labels rather than the domain names used in this document. Both print every configured trait, including any left unrated, so the sheet does not silently shorten when a teacher skips a row.

| Field | Source | Format and example |
| --- | --- | --- |
| trait_name | Snapshot | Text as configured. Punctuality. Neatness. Handwriting. |
| trait_rating | Stored | A whole number from 1 to 5 against each trait, printed beside the trait name as a single digit. This matches the scale already seeded in 6.2.7. A school that prefers word labels, or a letter scale, can reconfigure the scale in settings; the field holds whichever point value the school has configured, and the sheet renders it the same way either time. |
| trait_scale_legend | Snapshot | The meaning of each point on the scale, printed once beside the block rather than repeated per row, from the `point_label` values in 6.2.7. For the seeded default: 5 - Maintains an excellent degree of observable traits. 4 - Maintains a high level of observable traits. 3 - Shows an acceptable level of observable traits. 2 - Shows minimal regard for observable traits. 1 - Shows no regard for observable traits. |
| unrated_trait | Computed | A blank cell. Never a default rating and never an omitted row. |

## C.5 Attendance block

| Field | Source | Format and example |
| --- | --- | --- |
| days_school_opened | Stored | Integer, entered per arm per term. 62. |
| days_present | Stored | Integer per pupil. 58. |
| days_absent | Computed | days_school_opened minus days_present. Never typed, so the three figures cannot disagree. |
| attendance_percentage | Computed, optional | One decimal place with a percent sign. 93.5%. |

Section 3.2 puts daily attendance registers out of scope. What this block carries is the termly total the sheet prints, typed once per pupil, and nothing in the product infers it from a register that does not exist.

## C.6 Remarks and signature block

| Field | Source | Format and example |
| --- | --- | --- |
| class_teacher_remark | Stored | Text up to 300 characters, printed as typed, wrapping to as many lines as it needs. |
| class_teacher_name | Stored | The full name of the account that wrote the remark, captured at the time of writing so a staff change later does not rewrite an issued sheet. |
| head_teacher_remark | Stored | Text up to 300 characters. Required before publication. |
| head_teacher_name | Snapshot | From settings, not from the signed-in account, since the sheet carries the office rather than the typist. |
| head_teacher_signature | Snapshot, optional | Image rendered at a fixed height. Where absent, a ruled signature line prints instead so the sheet can be signed by hand. |
| date_issued | Computed | DD/MM/YYYY, the publication date, not the print date. |
| school_stamp_area | Layout | A reserved blank area of at least 35 mm square. Schools stamp these sheets and a layout with no room for the stamp gets stamped over the remarks. |

## C.7 Footer block and verification

| Field | Source | Format and example |
| --- | --- | --- |
| grading_key | Snapshot | The full band table as frozen: letter, range, descriptor. Printed on every sheet, because a parent reading a B needs to see what a B means and because a sheet reprinted after a boundary change must show the boundaries it was graded against. |
| assessment_key | Snapshot, optional | The component names with their maximum marks, so the column headings can be abbreviated. |
| revision_number | Stored | Integer. Printed only where above 1, as Revision 2. |
| verification_token | Stored | A 12 character token, printed in groups of four, resolving to a page that confirms a sheet was issued by the school and shows the pupil's initials, class, session, term and average, and nothing else. |
| verification_qr | Computed, optional | A QR code encoding the verification URL, printed beside the human-readable token so a sheet remains verifiable where a camera fails. |
| generated_timestamp | Computed | DD/MM/YYYY HH:MM in West Africa Time, labelled Printed, distinct from date_issued. |
| page_number | Layout | Page 1 of 1, or 1 of 2 where the subject count forces a second page. |
| printed_by | Computed | The account name, on sheets printed inside the school only. Absent from the parent's downloaded copy, per 9.10. |

## C.8 Rules that bind every renderer

These apply to the interface preview, the school's printed PDF, the parent's downloaded PDF and any future template the school supplies.

1. No renderer computes anything. Every number on the sheet is read from the stored computed row, so the preview, the school's print and the parent's download cannot disagree.
2. No renderer reads live settings for a published sheet. School name, grading bands, component maximums and the head teacher's name all come from the snapshot.
3. An absent value renders as an absence. ABS for a missed examination, a blank cell for an unentered mark, an omitted line for an optional field that is off. Nothing is filled with a zero or a dash that could be read as a score.
4. The sheet is readable in monochrome. Nothing depends on colour, and a grade band is identified by its letter rather than by a shade.
5. The sheet fits one A4 page where the subject count allows, and where it does not, the break falls between blocks and never inside the subject table. The remarks and signature block is never orphaned onto a page of its own.
6. Class figures are printed alongside their number_counted, so an average of 59.7 over 27 pupils in an arm of 28 is self-explaining.
7. Every ordinal names its cohort. There is no bare position on any sheet produced by this system.
8. The parent's downloaded PDF is under 200 KB, per 6.9.6, and carries no photograph unless the school switched it on.

## C.9 On the reference sample supplied

The sample was a screenshot of a published online result for a pupil at Little Angels High School, Owerri, not a document from Golden Royal Ark School. It answers the shape question this appendix could not answer on its own, and it is treated accordingly: adopted where it shows a layout convention, ignored where it shows that school's own configuration or its own software's defects.

| Taken from the reference | Not taken from the reference |
| --- | --- |
| The header groups identity, attendance and standing into one dense two-column band beneath the letterhead, with the photograph at top right, per C.1. | Its colour scheme and crest. GRAS prints its own school_name, school_logo and school_address, per C.1, in GRAS's own branding rather than the reference school's red and green. |
| The subject table prints one grade column and one comment column per subject, matching subject_grade and grade_descriptor, and shows only class_average per subject rather than the full comparative set, per C.2. | Its assessment structure of two summative components at 20 percent each plus an examination at 60. GRAS's own seed data of two continuous assessments and an assignment totalling 40, plus an examination at 60, governs component_score and ca_total, per 6.2.7. The reference's column count is a layout fact; the marks behind it are not. |
| The Skills and Behaviour side panels' printed legend as a full sentence per scale point, per C.4 and Appendix A entry 48. GRAS already defaulted to a five-point numeric scale before the sample arrived. | Its trait list. Nineteen items appeared across the two reference panels; GRAS's own trait list is set through Appendix B question 10 and 6.2.7, and may be shorter or differently named. |
| The cumulative three-term summary and the promotion status printed on the terminal sheet itself rather than only on a separate annual document, per the summary block above and Appendix A entry 46. | Its grading scale of Distinction, Credit, Pass, Weak Pass and Fail against a 0 to 100 range different from GRAS's own. GRAS keeps the seeded A to F scale in full, and overall_descriptor and grade_descriptor are drawn from GRAS's own snapshot, never from the reference's bands. |
| The per-core-subject pass line beneath the promotion status, per core_subject_status above. | Its position field, which printed a cohort size of zero, and its age field, which printed the session year rather than a computed age. Both are software defects in the reference, not conventions, and are called out again in C.1 so a future reader does not mistake them for a requirement. |
| A printed grading key on the sheet itself, which the reference calls Grade Details and this document already specified independently as grading_key in C.7. | Its wording, for example Principal in place of Head Teacher, and its literal Pass/Fail label standing in for what this contract calls overall_descriptor. GRAS uses the terms defined in Appendix D throughout. |

Nothing above changes a computation, an entity or an endpoint. Every field named in the left column already existed in this appendix or in section 6.7 before the sample arrived; what the sample changed is which of those fields the default layout shows, in what grouping, and which are held back for the broadsheet. That is consistent with 6.9.7: a template answers a layout question, and a layout question is answered by rearranging the contract, not by extending it.


---

---

## C.10 Status of this appendix: superseded for field layout

The school has now supplied **its own** result sheets, which this appendix was written in the absence of. Appendix C is therefore superseded for field-level layout and remains authoritative only for its general rules.

**Use instead:**

| For | Read |
|---|---|
| Nursery pupils | `21-appendix-e-nursery-result-sheet.md` |
| Primary pupils | `22-appendix-f-primary-result-sheet.md` |
| Weekly reports | `23-appendix-g-weekly-report-contract.md` |

**Still authoritative in this appendix:**

- The three-value source model in the introduction. Stored, Computed and Snapshot mean the same things in Appendices E, F and G.
- C.8, the rules binding every renderer. All eight apply unchanged to the new sheets, with one correction: rule 6 required class figures to print alongside their `number_counted`, and since the school's sheets print no class figures at all, that rule now applies only to the broadsheet.
- C.9, the record of what was and was not taken from the Little Angels sample, kept as history so a future reader understands why some earlier decisions were made.

**No longer authoritative:** C.1 through C.7, the field-by-field layout. The header grouping described in C.1 was the Little Angels arrangement and is not the school's. The inline cumulative block added to the summary block per Appendix A entry 46 is not on the school's sheets and is not printed.

Nothing in the data model is discarded by this. Every field named in C.1 through C.7 either appears on one of the school's sheets, appears on the broadsheet in section 10, or is retained as an optional block the school can switch on. The change is which fields the default renderer prints, not which fields exist.
