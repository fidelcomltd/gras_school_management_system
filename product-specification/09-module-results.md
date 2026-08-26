## 6.7 Result Recording, Computation, Approval and Publication

### 6.7.1 Purpose

Every operation in this module is per arm. A score sheet covers one arm. A computation covers one arm. An approval covers one arm. A publication covers one arm. There is no operation anywhere in this module that takes a class level as its subject, and any endpoint that appears to is a defect.

The unit of work is the result set: one row per arm per term, carrying the state, the recompute flag and, once published, the configuration snapshot. The class teacher works inside it, the head teacher approves it, and it is the thing that becomes visible to parents.

### 6.7.2 Actors and required privileges

| Operation | Privilege |
| --- | --- |
| Open a score sheet | `result.view`, arm-scoped |
| Enter or edit marks | `result.score.enter`, arm-scoped, and the result set must be Draft or Returned for Correction |
| Void marks for a subject | `result.score.void`, Super Admin, reason required |
| Enter trait ratings | `result.trait.enter`, arm-scoped |
| Enter attendance | `result.attendance.enter`, arm-scoped |
| Write the class teacher's remark | `result.remark.classteacher`, arm-scoped |
| Write the head teacher's remark | `result.remark.headteacher`, school-wide |
| Run computation | `result.compute` |
| Submit for approval | `result.submit`, arm-scoped |
| Approve or return | `result.approve`, `result.return`, school-wide |
| Publish | `result.publish`, school-wide |
| Withdraw a published set | `result.unpublish`, Super Admin, reason required |
| Compute annual cumulative results | `result.annual.compute` |
| Override a proposed promotion status | `promotion.decide` |

### 6.7.3 Entities

#### result_set

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| id | UUID | Yes | System. |
| arm_id | UUID | Yes | Unique together with term_id. One result set per arm per term. |
| term_id | UUID | Yes | Must belong to the arm's session. |
| state | Enum | Yes | Draft, Awaiting Approval, Approved, Published, Returned for Correction, Withdrawn. |
| needs_recompute | Boolean | Yes | Set by any event that invalidates the computed rows: a mark change, a pupil transfer in or out, a subject mapping change, a settings change under 6.2.9. Cleared by a successful computation. |
| computed_at, computed_by | Timestamp, UUID | No | Written by computation. |
| submitted_at, submitted_by | Timestamp, UUID | No | Written on submission. |
| approved_at, approved_by | Timestamp, UUID | No | Written on approval. |
| published_at, published_by | Timestamp, UUID | No | Written on publication. |
| revision_number | Integer | Yes | 0 until first publication, then 1, incremented on each republication after a withdrawal. |
| return_reason | String 500 | No | Set when returned. Cleared on the next submission. |
| config_snapshot | JSONB | No | Written once on first publication. Never overwritten on republication: a second snapshot row is written and the current one referenced, so the history of what each revision looked like survives. |
| config_version_id | UUID | No | Written with the snapshot. |
| pupil_count | Integer | No | Number of pupils ranked, written by computation. Printed on the result sheet as the denominator of the position. |

#### subject_score

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| id | UUID | Yes | System. |
| result_set_id | UUID | Yes | Denormalised for query speed. Derivable from pupil and term. |
| pupil_id | UUID | Yes | Unique together with subject_id and term_id. |
| subject_id | UUID | Yes | Must be in the set of subjects in effect for the arm this term. |
| term_id | UUID | Yes | Marks are always term-bound. |
| component_marks | JSONB | Yes | A map from assessment component id to an integer mark, for the non-examination components. Every component in the structure must be present as a key. A missing key is a validation failure, not an implicit zero. |
| exam_mark | Integer | Cond | Required unless exam_absent is true. 0 to the examination maximum from settings. |
| exam_absent | Boolean | Yes | Defaults false. True means the pupil did not sit the examination. |
| entered_by, entered_at | UUID, Timestamp | Yes | The account that last wrote the row. |
| voided_at, voided_by, void_reason | Timestamp, UUID, String | No | A voided row is excluded from computation and retained. |

Computed tables `subject_result_line`, `subject_arm_statistic` and `pupil_term_result` hold the outputs described in 6.7.6, together with `trait_rating` holding one row per pupil per trait per term and `attendance_entry` holding times present and times absent per pupil per term. Computed rows are deleted and rewritten wholesale on each computation.

### 6.7.4 Score entry

The class teacher picks a term, an arm and a subject. She gets one sheet: every active pupil in that arm as a row, the continuous assessment components as columns in the order set in settings, and the examination as the last column, with a running subject total column that updates as she types.

| Element | Behaviour |
| --- | --- |
| Row order | Surname ascending. Fixed, and the same every time she opens the sheet, because she is reading from a paper mark book in that order. Row order is never affected by marks. |
| Row identity | Registration number and full name. Photograph thumbnail is available on hover but not in the row, because it slows the table on a weak connection. |
| Columns | One per non-examination component with the short label and the maximum in the header, for example CA1 / 15. Then EXAM / 60. Then Total / 100, read-only. |
| Keyboard behaviour | Tab and Enter move down the column, not across the row, because a teacher enters one column at a time from a mark book. Arrow keys move in the obvious directions. This detail decides whether the screen is usable. |
| Save | Explicit Save draft button, plus an automatic save every thirty seconds and on navigating away. The automatic save is silent on success and shows a persistent banner on failure with a Retry action, because a teacher who loses forty minutes of typing on a dropped connection will not use the system again. |
| Absent | A checkbox in the EXAM cell labelled Abs. Ticking it clears and disables the mark field for that cell. |
| Void | A Super Admin action on the sheet header voiding every mark for this subject and arm this term, with a reason. Used only to unwind an error. |

#### Validation on save

- Each component mark is an integer between 0 and that component's maximum from settings. Rejection is per cell, inline, in red, naming the maximum: Maximum for CA1 is 15.
- Negative values, decimals and text are rejected at the cell.
- A blank cell is blank. It is never treated as zero. The sheet shows blanks as empty, the completeness gate counts them as missing, and the pupil cannot be submitted with one. A pupil who genuinely scored nothing has 0 typed into the cell, and the two are stored differently and behave differently.
- The examination cell is either a mark or ticked absent. Both empty is incomplete. Both filled is impossible, because ticking absent disables the field.
- Saving a draft does not require completeness. A teacher can enter CA1 for the whole arm on Monday and CA2 on Thursday.
- The whole sheet saves in one transaction. A single invalid cell rejects the whole save with the cell highlighted, rather than saving twenty-seven rows and losing the twenty-eighth.
- Entry is refused with 409 when the result set is Awaiting Approval, Approved, Published or Withdrawn, when the term is closed, or when the arm's session is closed.

#### Absent from the examination, against a score of zero

These are different facts and the system keeps them different.

| Case | Treatment |
| --- | --- |
| Pupil scored 0 in the examination | `exam_mark` is 0, `exam_absent` is false. The sheet prints 0. The pupil counts in the subject's class average, highest and lowest, and in position ranking. Their subject total is their continuous assessment total plus zero. |
| Pupil was absent for the examination | `exam_absent` is true, `exam_mark` is null. The result sheet prints ABS in the examination column. The subject total is the continuous assessment total alone, and the sheet prints that total with an asterisk keyed to a footnote reading ABS: absent for the examination. The pupil is included in position ranking with that reduced total, because the school still ranks them and the parent still expects a position. The pupil is excluded from the subject's class average, highest and lowest, so one absentee does not drag the class figure the whole arm is measured against. |

The asymmetry is deliberate and is logged in Appendix A entry 26. Including an absentee in the ranking is fair to the other pupils, whose positions would otherwise be flattered. Excluding them from the class average is fair to the arm, whose average would otherwise be depressed by a fact about attendance rather than attainment. Where a pupil is absent for the examination in every subject, the head teacher should consider withholding the result rather than publishing a sheet of ABS, and the approval screen flags such pupils.

### 6.7.5 Completeness gate

Before a result set can be submitted, every pupil active in the arm must have a complete `subject_score` row for every subject in effect for that arm this term, every trait must be rated for every pupil, attendance must be entered for every pupil, and the class teacher's remark must be present for every pupil.

Completeness is surfaced as a grid, not a list of errors. The arm's readiness screen shows subjects across the top and pupils down the side, with a tick for a complete row, a partial marker showing how many of the components are filled, and an empty cell for nothing entered. Beneath it, four counters: marks 246 of 252 cells complete, traits 28 of 28 pupils, attendance 26 of 28 pupils, class teacher remarks 24 of 28 pupils. Each counter links to the screen that fills the gap, filtered to the incomplete rows.

The submit button is disabled with the reason beside it: **6 mark cells, 2 attendance entries and 4 remarks are still missing. See the readiness grid.** Attempting submission by endpoint returns 422 with a structured list of what is missing, so the interface can present it without a second call.

A pupil whose status changed to withdrawn or transferred during the term, with an effective date inside the term, is excluded from the gate and from the result set entirely from that date. The readiness grid shows them in a separate collapsed row group labelled Left during the term, so the teacher can see they were not overlooked.

### 6.7.6 Computation

Computation is a single operation over one result set. It deletes every computed row for that result set and rewrites them. It reads the live settings when the set is not published and the snapshot when it is. It is idempotent: running it twice on unchanged inputs produces identical output.

#### Per pupil per subject, written to subject_result_line

| Value | Rule |
| --- | --- |
| ca_total | Sum of the non-examination component marks. |
| exam_mark | As entered, or null when absent. |
| subject_total | ca_total plus exam_mark. When exam_absent is true, ca_total alone. |
| grade | The grade_letter of the band where lower_bound is less than or equal to subject_total and subject_total is less than or equal to upper_bound. Resolved against the scale in force for this result set. A total that matches no band is impossible once the coverage rules in 6.2.5 are enforced, and if it happens the computation fails loudly rather than writing a blank grade: Computation stopped. Mark 100 in Mathematics for GRAS/2026/0041 matches no grading band. Check the grading scale. |
| remark | The remark text of the same band. |
| subject_position | Competition rank on subject_total descending within the arm. Ties share a position and the next position skips the tied count, so two pupils on 78 are both 6th and the next is 8th. |
| is_pass | True when subject_total is at or above `pass_mark` from settings. Not printed, used by promotion. |

#### Per subject per arm, written to subject_arm_statistic

| Value | Rule |
| --- | --- |
| highest_score | Maximum subject_total among counted pupils. |
| lowest_score | Minimum subject_total among counted pupils. |
| class_average | Mean of subject_total among counted pupils, rounded to one decimal place, half up. |
| counted_pupils | Pupils active in the arm with a non-voided score row and exam_absent false. Absentees are excluded, per 6.7.4. |

#### Per pupil, written to pupil_term_result

| Value | Rule |
| --- | --- |
| subjects_taken | Count of subjects in effect for the arm this term. |
| total_obtainable | subjects_taken multiplied by 100. |
| total_obtained | Sum of subject_total across the pupil's subjects. |
| average | total_obtained divided by subjects_taken, rounded to two decimal places, half up. |
| overall_grade | The band matching the rounded average. Printed beside the average. |
| arm_position | Competition rank on total_obtained descending within the arm. Every pupil in an arm takes the same subject set, so total_obtained is directly comparable and no normalisation is needed. |
| arm_pupil_count | Number of pupils ranked. This is the denominator printed on the sheet, and it counts active pupils only. |
| level_position | Written only when `show_level_position` is true or `primary_position_scope` is level. Competition rank on average descending across every pupil in every arm of the same level in the same term. Average rather than total, because two arms of the same level may take different subject sets through exceptions and totals would then be incomparable. |
| level_pupil_count | Number of pupils across the level, written with level_position. |

#### The arm question, settled

Class average, highest score, lowest score, subject position and overall position are computed within the arm. Not within the level. The arm is the cohort the sheet names at the top and the group the child sits with every day, and a position computed over a group the parent cannot see is a number nobody can check.

Where `show_level_position` is true, the level figure is computed alongside and printed as a separate labelled line, never in the same box as the class position. The sheet reads:

> **Position in Class (Primary 2C, 22 pupils): 4th**

> **Position in Primary 2 (all 3 arms, 76 pupils): 11th**

Two lines, each naming its cohort and its size explicitly. Never a bare 4th and an 11th side by side. The fairness consequence is real and is recorded in Appendix A entry 30: a pupil ranked 1st in a weak arm and a pupil ranked 4th in a strong arm are not comparable, and parents will compare them anyway. Printing the level position is how the school gets ahead of that conversation rather than being ambushed by it in the car park.

#### Tie-breaking and ordinal display

| Rule from settings | Behaviour |
| --- | --- |
| shared_position, the default | Tied pupils receive the same ordinal. The next position skips by the number tied. Two pupils on 320 are both 3rd and the next pupil is 5th. On the result sheet the ordinal is followed by the word tied in brackets: 3rd (tied). The word appears only where a tie exists. |
| exam_then_ca | Ties on total_obtained are broken by the higher total examination marks across all subjects, then by the higher total continuous assessment marks. A tie surviving both is shared. |
| exam_then_alphabetical | As above, then by surname ascending. This rule never shares a position, which some schools prefer for prize-giving. |

Ordinals are formatted 1st, 2nd, 3rd, 4th through 10th, 11th, 12th, 13th, then by last digit with the teens exception, so 21st, 22nd, 23rd, 24th and 111th. This is a single formatting function, used in the interface, in the PDF and in every export.

Positions count active pupils only. A pupil withdrawn in week nine is neither ranked nor counted in the denominator.

`min_subjects_for_position` from settings, default 1, excludes a pupil with fewer scored subjects than the threshold from ranking. Such a pupil's sheet prints Not ranked in the position field with a footnote reading Not ranked: fewer than the minimum number of subjects recorded. Their marks, grades and average all still print.

#### Rounding, stated once

All rounding is half up. Averages per pupil are stored and printed to two decimal places. Class averages per subject are stored and printed to one decimal place. Grades are resolved against the unrounded subject total, which is always an integer, and against the rounded average for the overall grade. No other rounding happens anywhere, and no display layer rounds a second time.

### 6.7.7 Non-academic input

| Input | Who enters it | Detail |
| --- | --- | --- |
| Affective ratings | Class teacher, `result.trait.enter` | One rating per pupil per active affective trait, on the configured scale. Entered on a grid with pupils down the side and traits across the top, selected from a dropdown or by typing the scale value. A Fill column action sets one value down a whole column, because most of an arm is rated 4 for Punctuality and the teacher then changes the six who are not. |
| Psychomotor ratings | Class teacher, `result.trait.enter` | Same screen, second grid. |
| Attendance | Class teacher, `result.attendance.enter` | Times present and times absent per pupil. Times school opened comes from the term record and is not per pupil. Validation: present plus absent must equal times school opened, rejected otherwise with 24 present plus 3 absent is 27. School opened 58 times this term. A Fill action sets a value down the column. |
| Class teacher's remark | Class teacher, `result.remark.classteacher` | Free text up to 240 characters per pupil. A template picker offers phrases the school has saved, and inserting one puts editable text in the box rather than a locked value. Templates are school-managed and stored as a simple list, editable by anyone holding `result.remark.classteacher`. |
| Head teacher's remark | Head teacher, `result.remark.headteacher` | Free text up to 240 characters per pupil, written on the approval screen where the head teacher is already reading the sheet. The same template mechanism applies. A Fill action can set one remark down the whole arm, which is what actually happens for twenty of twenty-eight pupils. |

Remark templates are allowed and expected. A head teacher writing twenty-eight distinct sentences will write nothing, and the school's current paper practice is a rubber stamp of four phrases. Templates make the honest practice visible and leave room for the six pupils who need something said. The template list ships empty.

### 6.7.8 Approval

Approval is a separate step from computation and requires `result.approve`, which the class teacher does not hold. The head teacher opens the arm's computed sheet as one screen: the broadsheet of every pupil against every subject with totals, grades and positions, the per-subject class statistics, a distribution summary showing how many pupils fell in each grade band, and any flags the system has raised.

Flags shown on the approval screen, each with a link to the row:

- A pupil absent for the examination in more than half their subjects.
- A subject whose class average is below the pass mark, which usually means a marking error rather than a weak class.
- A subject where every pupil scored the same total, which means a column was filled down and not corrected.
- A pupil whose average moved by more than 25 marks from the previous term, which catches transposed marks.
- A pupil with no class teacher's remark, which the completeness gate would have caught, shown again because the head teacher is the last reader.

The head teacher then either approves or returns.

Approving moves the set to Approved, writes `approved_at` and `approved_by`, and locks marks against further editing. It does not publish and nothing becomes visible to parents.

Returning moves the set to Returned for Correction and requires a reason of at least ten characters, which is stored on the result set and shown to the class teacher at the top of her score sheet: **Returned by Mr Bello on 14/12/2026: Mathematics examination marks for the whole class look 10 marks too low. Check against the mark book.** The class teacher's `result.score.enter` privilege becomes effective again for that set, she corrects, computation is rerun, and she resubmits, which clears the return reason and moves the set back to Awaiting Approval. A set can be returned any number of times and each return is a separate audit event.

An Approved set can also be returned, for the case where the head teacher approves and then notices something before publishing.

### 6.7.9 Publication

Publication is per arm per term and requires `result.publish`.

#### Preconditions, each with its own block message

- The set is Approved.
- `needs_recompute` is false.
- Every pupil has a head teacher's remark: 3 pupils have no head teacher remark. Add them before publishing.
- The school logo and the head teacher's signature image are present in settings.
- The term's `next_resumption_date` is set: Next term's resumption date is not set. Results print it in the footer. Set it on the term before publishing.
- The term's `times_school_opened` is set.

#### What publication does

1. Writes the configuration snapshot: grading scale, assessment structure, trait lists, trait scale, result rules, school name, short name, motto, logo reference, head teacher name, signature reference, level name and the composed arm display name, all as they stand at this moment.
2. Sets state to Published, writes `published_at` and `published_by`, and increments `revision_number` to 1 on first publication.
3. Makes the result readable on the parent portal for every pupil in the set, to anybody presenting a valid pin and that pupil's registration number.
4. Writes an audit event naming the arm, the term, the pupil count and the snapshot's config version.

#### Is publication reversible?

Yes, by withdrawal, and it is deliberately awkward. `result.unpublish` is held by a Super Admin only and requires a reason. Withdrawal moves the set to Withdrawn, removes it from the parent portal immediately, and leaves the snapshot in place. A parent who looks up a withdrawn result sees the copy in 6.9.4 for that case, not a blank page and not an error.

From Withdrawn, a Reopen for correction action moves the set to Draft, which requires the term to be active or reopened per 6.3.6. Marks become editable, the set goes back through submission, approval and publication, and on republication `revision_number` increments to 2 and a second configuration snapshot is written alongside the first rather than replacing it.

Parents see a revision notice. A result set with `revision_number` above 1 renders a line under the header on screen and on the PDF: **Revised result, issued 19/01/2027. This replaces the version issued 14/12/2026.** The notice is not optional and cannot be suppressed by a setting. A school that quietly reissues a corrected result and a parent who has two different sheets with the same date is a dispute the product should not create.

A result corrected after publication therefore always produces a visible revision. There is no path that edits a published mark in place.

### 6.7.10 Annual cumulative result

Available per pupil once Third Term is published for the arm that pupil is enrolled in at the end of the session. Computed by `result.annual.compute`, run per arm, and stored in `annual_result`.

| Value | Rule |
| --- | --- |
| term_averages | The three `pupil_term_result.average` values for the pupil in the session. |
| cumulative_average | Under simple_average, the mean of the three term averages, rounded to two decimal places. Under weighted, the sum of each term average multiplied by its weight, divided by 100, rounded to two decimal places. |
| cumulative_grade | The band matching cumulative_average, resolved against the snapshot on the Third Term result set. |
| annual_position | Competition rank on cumulative_average descending within the arm the pupil ended the session in. |
| annual_pupil_count | Pupils in that arm with a complete set of three term results. |
| per_subject_annual | Per subject, the mean of the pupil's three subject totals, with a grade. Printed as an annual subject column where the school's sheet has room. Subjects taken in fewer than three terms show the mean of the terms taken, with a footnote. |
| promotion_status | Proposed by the rules in 6.3.7 and stored here. Editable by `promotion.decide` with a reason. |

#### Pupils who do not fit the simple case

| Case | Treatment |
| --- | --- |
| Joined mid-session and has two term results | The cumulative average is computed over the terms actually sat, with the divisor reduced accordingly. The sheet prints Cumulative average based on 2 of 3 terms. Under the weighted method the weights of the terms sat are rescaled to total 100 and the sheet prints the same footnote. The pupil is included in the annual ranking, because excluding them puts a child with a strong two terms outside the prize list for a reason the parent will not accept. Appendix A entry 33. |
| Joined in Third Term with one term result | One term is not an annual result. `annual_result` is written with cumulative_average equal to the single term average, `annual_position` null, and the sheet prints Not ranked annually: first term at this school. Promotion status is proposed on the single term. |
| Changed arms mid-session | The annual result is computed in the arm the pupil ended the session in. Term averages travel with the pupil, because they are attributes of the pupil and the term, not of the arm. The pupil is ranked against the pupils of their final arm. |
| Repeating the same level | Nothing special. A new session, new arm, new result set. The previous year's results remain under the previous session and both are readable on the portal. |
| Missing one term because the school did not score that arm | Treated as terms actually sat, as in the first row. If no term in the session has a published result, no annual result is written and the promotion review screen flags the pupil for a manual decision. |

Promotion status is decided here, on the Third Term and annual result, not in the promotion module. The promotion module reads it. This keeps the decision beside the numbers that justify it.

For the parent portal, the term selector orders First Term, Second Term, Third Term, then Annual Cumulative. Annual Cumulative is not selectable until Third Term is published for the pupil's arm and the annual computation has run. Before that it is present but greyed with the label Available after Third Term results are released.

Once the annual computation has run, the Third Term terminal sheet itself also carries the cumulative figures beneath its own summary block: the three term totals side by side, the grand total, the cumulative average and grade, the annual position, the promotion status, and a pass line for each core subject. This is in addition to the separate Annual Cumulative document, not instead of it, and it means a parent reading the Third Term sheet alone already sees the year's outcome rather than needing a second document. The exact fields and their source are in Appendix C, and the decision is recorded in Appendix A entry 46. If Third Term is published before the annual computation has run, the sheet ends at its own summary block and the cumulative section appears on republication or on a later render once the computation exists, without anybody needing to reprint by hand.

### 6.7.11 State machine

States: Draft, Awaiting Approval, Approved, Published, Returned for Correction, Withdrawn. A result set that does not exist yet is shown in the interface as Not started, which is the absence of a row rather than a state.

| From | To | Actor privilege | Trigger, preconditions and effects |
| --- | --- | --- | --- |
| Not started | Draft | `result.score.enter` | First save of any mark, trait, attendance or remark for the arm and term. Preconditions: term active, arm active, at least one subject in effect for the arm, form teacher not required yet. Effect: creates the result set with needs_recompute true. |
| Draft | Draft | `result.compute` | Computation. Preconditions: at least one complete score row. Effect: rewrites the computed tables, clears needs_recompute. Does not change state. |
| Draft | Awaiting Approval | `result.submit` | Submission. Preconditions: completeness gate passed in full per 6.7.5, needs_recompute false, computation has run since the last change, arm has a form teacher with an active account, term has times_school_opened. Effect: locks marks, traits, attendance and the class teacher's remark against editing. |
| Awaiting Approval | Approved | `result.approve` | Approval. Preconditions: none beyond the state. Effect: writes approved_at and approved_by. |
| Awaiting Approval | Returned for Correction | `result.return` | Return. Preconditions: reason of at least ten characters. Effect: stores the reason, reopens the marks for the class teacher. |
| Approved | Returned for Correction | `result.return` | Late return before publication. Same preconditions and effects. |
| Approved | Published | `result.publish` | Publication. Preconditions in 6.7.9. Effects in 6.7.9, including the configuration snapshot. |
| Returned for Correction | Awaiting Approval | `result.submit` | Resubmission. Preconditions as for the first submission, plus needs_recompute false. Effect: clears return_reason. |
| Returned for Correction | Draft | System | Automatic, when a pupil transfer or a settings change sets needs_recompute. Effect: the set drops back so that computation must be rerun before resubmission. |
| Published | Withdrawn | `result.unpublish` | Withdrawal. Preconditions: Super Admin, reason of at least ten characters. Effect: removed from the parent portal immediately, snapshot retained. |
| Withdrawn | Draft | `result.score.enter` plus `result.unpublish` | Reopen for correction. Preconditions: term active or reopened. Effect: marks editable, needs_recompute set true. |
| Any state | Same state with needs_recompute true | System | Triggered by a mark edit, a pupil transfer in or out, a subject mapping change, or a settings change under 6.2.9. A Published set is never flagged, because it renders from its snapshot and is not recomputed. |

Two rules cover the whole machine. Marks are editable in Draft and Returned for Correction and in no other state. Parents can read a set in Published and in no other state.

### 6.7.12 Error and edge cases

| Case | Behaviour |
| --- | --- |
| Computation run on a set with no complete score rows | Rejected: No complete marks have been entered for this class. Enter marks before computing. |
| Every pupil in the arm absent for one subject's examination | counted_pupils for that subject is zero, so class average, highest and lowest are printed as blank with a footnote reading No examination sat. Computation does not divide by zero and does not fail. |
| An arm with one pupil | Position is 1st of 1. Class average equals that pupil's total. Highest equals lowest. All correct, no special case, and the sheet does not hide the position. |
| An arm with zero active pupils at computation time | Computation is refused: This class has no active pupils. Nothing to compute. |
| A subject mapped mid-term after marks were entered for others | The new subject appears as a column, empty for everybody. The completeness gate blocks submission until it is filled or the mapping is ended for the term. |
| A mark edited after computation but before submission | needs_recompute is set. Submission is blocked with: Marks have changed since the last computation. Run computation again before submitting. |
| Grading scale edited while the set is Approved | needs_recompute is set per 6.2.9. The set falls back to needing computation and cannot be published until it is rerun. This is the mechanism that stops a term being half-graded on two scales. |
| Two class teachers on the same sheet at once | Last write wins per cell, with optimistic concurrency at the sheet level. The second save is rejected with: This sheet was changed by Mrs Adeyemi while you were working. Reload to see her entries, then re-enter yours. Both attempts are in the audit log. |
| Head teacher publishes while the class teacher is still typing | Impossible. Marks are locked from submission onward, so the class teacher's session was already read-only. |
| Result set submitted, then a pupil is transferred in | needs_recompute is set and the set drops from Awaiting Approval to Draft automatically with a system note. The head teacher's approval queue shows the change rather than silently losing the item. |
| Annual computation run before all three terms are published | Rejected, naming what is missing: Second Term results for Primary 2C are not published. Publish all three terms before computing annual results. |
| PDF requested for a set that is not published | Permitted inside the back office with `result.print`, and the PDF is watermarked DRAFT diagonally across the page. The parent portal cannot reach it at all. |

### 6.7.13 Endpoints

| Endpoint | Notes |
| --- | --- |
| GET /result-sets?term_id=&arm_id= | State, counts, flags. The head teacher's queue calls this with no arm filter. |
| GET /score-sheets?arm_id=&subject_id=&term_id= | The sheet: pupils, components with maximums, existing marks, totals. |
| PUT /score-sheets | Whole-sheet save in one transaction. Returns per-cell validation failures. |
| POST /score-sheets/void | Super Admin, reason required, per subject per arm per term. |
| GET /result-sets/{id}/readiness | The completeness grid and the four counters. |
| PUT /result-sets/{id}/traits | Both trait grids for the arm. |
| PUT /result-sets/{id}/attendance | Times present and absent per pupil. |
| PUT /result-sets/{id}/remarks/class-teacher | Per pupil. |
| PUT /result-sets/{id}/remarks/head-teacher | Per pupil. Supports a fill-all body. |
| GET /remark-templates, POST /remark-templates, DELETE /remark-templates/{id} | The school's saved phrases. |
| POST /result-sets/{id}/compute | Idempotent. Returns the computed summary and any flags. |
| POST /result-sets/{id}/submit | Returns 422 with the structured missing-items list on failure. |
| POST /result-sets/{id}/approve | Head teacher. |
| POST /result-sets/{id}/return | Reason required. |
| POST /result-sets/{id}/publish | Writes the snapshot. Returns the blocking precondition on failure. |
| POST /result-sets/{id}/withdraw | Super Admin, reason required. |
| POST /result-sets/{id}/reopen | Withdrawn to Draft. |
| GET /result-sets/{id}/broadsheet | The whole arm as one table, for the approval screen and for export. |
| GET /pupils/{id}/results/{term_id} | One pupil's computed result, the payload behind Appendix C. |
| POST /arms/{id}/annual-results | Runs annual computation for the arm. |
| GET /pupils/{id}/annual-result?session_id= | The annual payload. |
| GET /pupils/{id}/results/{term_id}/pdf | Back-office PDF. Watermarked unless published. |


---

---

## 6.7.12 Amendment: section-specific result sheets

Added after the school supplied its own nursery and primary report sheets. **Design references:** `assets/02-nursery-termly-report-page1.png`, `assets/03-nursery-termly-report-page2.png`, `assets/04-primary-termly-report-page1.png`, `assets/05-primary-termly-report-page2.png`.

Everything specified above in 6.7 continues to apply to both sections: the result set, the state machine, the completeness gate, computation, approval, publication, snapshotting and the annual cumulative result are all identical for a nursery arm and a primary arm. What differs is **what is entered alongside the marks** and **what the sheet renders**.

### The two sheet variants

`class_level.section` decides which sheet a pupil gets. Full field contracts:

- Nursery: `21-appendix-e-nursery-result-sheet.md`
- Primary: `22-appendix-f-primary-result-sheet.md`

| | Nursery | Primary |
|---|---|---|
| Cognitive subject table | Yes, 14 seeded subjects | Yes, 19 seeded subjects |
| Assessment columns | Derived from the configured component list. Currently 1st CA 20, 2nd CA 20, Exam 60 | Identical, and identically derived |
| Rated blocks | Four development domains, 45 indicators, four-point E/S/I/N, **with a per-indicator comment** | Affective 11 traits and Psychomotor 8 traits, three-point E/I/N, no comments |
| Attendance printed | No | Yes, as Time School Opens, Time Present, Time Absent |
| Position or class average printed | No | No |
| Pages | 2 | 2 |
| Fee notice block | Yes | Yes |

### What this changes in this module

**6.7.4 Score entry** is unchanged. The score sheet is the same grid for both sections; only the subject list differs, and that already comes from the per-level subject mapping in 6.6.

**6.7.7 Non-academic input** splits by section:

- A **primary** arm's teacher enters affective and psychomotor trait ratings on the three-point scale, exactly as previously specified, and enters attendance.
- A **nursery** arm's teacher enters development indicator ratings on the four-point scale across four domains, plus an optional free-text comment per indicator, and enters attendance even though it does not print.
- The entry surface for nursery is a grid of indicators down the side and pupils across the top, with a `Fill row` action, because 45 indicators times 30 children is 1,350 cells and no teacher will complete that one dropdown at a time. The comment field opens on tap and is expected to be used sparingly.

**6.7.5 The completeness gate** requires, per section: every subject mark cell; every trait rating for primary or every indicator rating for nursery; attendance; the class teacher's comment; and the head teacher's comment. Indicator comments are optional. A nursery arm cannot be submitted with unrated indicators, because a blank row on a development sheet reads to a parent as a judgement withheld.

**Computation** is unaffected. Development ratings and trait ratings are never converted to marks, never averaged, never ranked. The term average is the grand total over the subject count, per Appendix E.8.

**Publication and the snapshot** must additionally freeze the section, the domain and indicator lists or the trait lists as they stood, the rating scale and its legend, and the fee notice lines, so a sheet reprinted after the school reorganises its indicators renders as originally issued.

### Superseded earlier decisions

- **Appendix A entry 46** adopted an inline three-term cumulative block on the Third Term sheet from the Little Angels sample. Neither of the school's own sheets has such a block, so it is not printed. The separate annual cumulative document in 6.7.10 remains. See `22-appendix-f-primary-result-sheet.md` F.7.
- **Appendix A entry 48** and the trait scale in 6.2.7 assumed one school-wide five-point numeric scale. Two scales are now needed. See `25-open-conflicts-to-resolve.md` item 6.
- The seeded grading scale and assessment structure both change. See `25-open-conflicts-to-resolve.md` items 1 and 2. These are the two items most expensive to get wrong, because the assessment structure locks for the session at the first mark entered.
