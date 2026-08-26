## 6.2 School Settings and Academic Configuration

### 6.2.1 Purpose

The result engine reads its rules from here. What a 68 is called, how many marks a Second CA Test is worth, which traits appear on the sheet, how a tie is broken, what the registration number looks like: all of it lives in this module and none of it is written in code. Treat this as the school's rulebook, not a preferences page, and note that a careless edit here can change what a parent reads on a result sheet.

### 6.2.2 Seed data: what exists on installation and what the admin must create

| Item | State on install | Notes |
| --- | --- | --- |
| School abbreviation | Seeded as `GRAS` | Editable. Frozen into each registration number at the moment of issue. |
| Nine class levels with progression chain | Seeded | Nursery 1 to Primary 6, chained, Primary 6 terminal. Fully editable, extendable, deactivatable, and deletable where nothing references them. |
| Sections Nursery and Primary | Seeded | The section list is itself editable, so a school adding a Creche section can. |
| Assessment structure | Seeded | First CA Test 15, Second CA Test 15, Assignment 10, Examination 60. |
| Grading scale | Seeded | Six bands, A to F, as set out in 6.2.6. |
| Affective trait list | Seeded | Punctuality, Neatness, Honesty, Politeness, Attentiveness, Relationship with Others, Self-Control. |
| Psychomotor trait list | Seeded | Handwriting, Drawing and Painting, Games and Sports, Handling of Tools, Musical Skills. |
| Trait rating scale | Seeded | Five points, 1 to 5, labelled Poor, Fair, Good, Very Good, Excellent. |
| Pin defaults | Seeded | Length 10, maximum uses 20, unambiguous character set. |
| Result rules | Seeded | Simple average annual method, arm-scoped position, level position shown, shared-position tie-breaking, pass mark 40, promotion threshold 40. |
| Six roles | Seeded | Super Admin, School Administrator, Head Teacher, Class Teacher, Bursar, Auditor. |
| Bootstrap Super Admin | Seeded by installer | One account, forced password change on first login. |
| School name, address, phone, email, motto, logo | Not seeded | Admin must supply. Logo and head teacher signature are image uploads. |
| Head teacher name and signature image | Not seeded | Required before a result can be published. |
| Arms | Not seeded, zero rows | Admin creates every arm, per level, per session, in whatever quantity that level needs. |
| Academic sessions and terms | Not seeded | Admin creates the session and its three terms with real dates. |
| Subjects | Not seeded | Admin creates them. The system ships with no subject list, because subject names vary and a wrong seeded list is worse than an empty one. |
| Subject to level mappings | Not seeded | Admin maps per session and term. |
| Roles beyond the seeded six | Not seeded | Optional. |
| Pupils and guardians | Not seeded | Admin registers or bulk imports. |
| Pin batches | Not seeded | Generated per arm when results are ready to release. |

#### First-run setup checklist

An administrator logging into an empty system cannot enter a single mark until the structure beneath it exists. The system renders this checklist on the dashboard until every step is complete, with each step showing done, in progress or blocked and a link straight to the screen. Steps 9, 10, 11, 13 and 14 are hard dependencies: score entry is unreachable until all five are done, and the score entry menu item, if reached by URL, returns a page naming the missing step rather than an empty sheet.

1. Change the bootstrap password. Forced, cannot be skipped.
2. Enter school identity: name, short name, address, phone, email, motto. Upload the logo.
3. Enter the head teacher's name and upload the signature image.
4. Confirm or change the abbreviation `GRAS`, then check the registration number preview reads the way the school wants it.
5. Review the grading scale. Change bounds, letters or remarks if the school's scale differs.
6. Review the assessment structure. Collapse to a single CA of 40 or move to CA 30 and Exam 70 if that is what the school uses.
7. Review the affective and psychomotor trait lists and the rating scale.
8. Set the result rules: annual method, whether a level position is shown, tie-breaking, pass mark, promotion threshold, core subjects.
9. Review the nine class levels. Deactivate any the school does not run. A school with no nursery deactivates Nursery 1, 2 and 3 here, which makes Primary 1 the entry level.
10. Create the academic session, for example 2026/2027, with three terms and their real start dates, end dates and resumption dates.
11. Open First Term.
12. Create admin accounts for the head teacher, the bursar and each form teacher.
13. Create arms. Use bulk creation to make one arm per active level in one action, then add extra arms where the intake needs them.
14. Assign each form teacher the Class Teacher role scoped to their arm.
15. Create subjects.
16. Map subjects to levels for the active session and term. The bulk mapping screen handles the common case of one subject list applying to all six primary levels.
17. Register pupils, singly or by bulk import, enrolling each into an arm.
18. Enter marks. From here the system is in normal operation.

Step ordering is enforced only where a genuine dependency exists. An administrator who insists on creating subjects before creating the session may do so, because a subject is not session-scoped. An administrator who tries to register a pupil before any arm exists is stopped, with the message: **No arms have been created. Create an arm under a class level before registering pupils.** That message is the difference between an administrator who blames the product and one who clicks the link in it.


---

### 6.2.3 School identity

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| school_name | String 160 | Yes | Free text. Appears in full on the result sheet header. |
| short_name | String 60 | Yes | Used where the full name will not fit, for example the pin slip. |
| abbreviation | String 8 | Yes | Uppercase letters and digits only, 2 to 8 characters. Defaults `GRAS`. Used in registration numbers. Editing it requires `settings.abbreviation.update` and triggers the warning in 6.2.4. |
| address | String 300 | Yes | Multi-line permitted. |
| phone | String 20 | Yes | Nigerian format, normalised as in 6.1.3. |
| email | String 160 | Yes | Valid email format. |
| motto | String 120 | No | Printed under the school name on the result sheet if present. |
| logo | Image | Yes | PNG or JPEG. Maximum 2 MB. Minimum 300 by 300 pixels. Stored at original plus a 200 pixel and a 64 pixel derivative. Required before publication. |
| head_teacher_name | String 120 | Yes | Printed above the head teacher's signature block. |
| head_teacher_signature | Image | Yes | PNG with transparency preferred. Maximum 1 MB. Recommended 600 by 200 pixels. Required before publication. |
| timezone | Fixed | Yes | Africa/Lagos. Not editable in this version. |

Editing school name, motto or logo does not affect published results, because the snapshot captured at publication holds the values that were in force. A parent downloading a 2026/2027 First Term result in 2029 sees the school name and logo as they were in December 2026. This surprises people, so the identity screen carries a line of text saying so.

### 6.2.4 Registration number configuration

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| separator | String 1 | Yes | One of / or - or . Defaults /. |
| serial_width | Integer | Yes | Between 3 and 6. Defaults 4. Serials are zero padded to this width. |
| serial_reset | Enum | Yes | per_year or continuous. Defaults per_year. |
| year_source | Fixed | Yes | Admission year. Not editable, because a number that changes meaning with the calendar is not an identifier. |

The screen shows a live preview built from the current abbreviation, separator and serial width, using the next serial that would actually be issued. With `GRAS`, /, width 4 and thirty-nine pupils already admitted in 2026, the preview reads `GRAS/2026/0040`. Changing the width to 5 updates the preview to `GRAS/2026/00040` before anything is saved.

Editing the abbreviation does not rewrite a single registration number that has already been issued. Numbers already in the register keep the abbreviation they were issued with, permanently. Numbers issued after the change carry the new abbreviation. The school will therefore hold two number formats side by side, and that is correct: a number is a historical identifier, not a derived field.

At the point of editing the abbreviation the system shows a dialogue naming the exact consequence, with the current count filled in:

> **412 pupils already hold registration numbers beginning GRAS. Those numbers will not change. Pupils registered from now on will receive numbers beginning GRA. Your register will contain both. Type CHANGE to continue.**

The confirmation requires the literal word CHANGE typed into a field, and the save writes an audit event with a mandatory reason. The serial counter is keyed on the admission year alone and not on the abbreviation, so changing the abbreviation part way through 2026 does not restart the serial at 1 and cannot produce two pupils whose numbers differ only by prefix.

### 6.2.5 Grading scale editor

A table the administrator edits in place. Each row is one band. Add a row, edit a row, remove a row, drag to reorder. Nothing about the seeded scale is protected: every bound, every letter and every remark can change, a seventh band can be added, and the scale can be reduced to four bands.

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| lower_bound | Integer | Yes | 0 to 100 inclusive. Whole numbers only. |
| upper_bound | Integer | Yes | 0 to 100 inclusive. Whole numbers only. Must be greater than or equal to lower_bound. |
| grade_letter | String 2 | Yes | Letters and digits. Unique across the scale, case-insensitive. |
| remark | String 40 | Yes | Free text, for example Excellent, Very Good, Fair, Fail. Appears on the result sheet in the Remark column. |
| display_order | Integer | Yes | System-maintained from the drag order. Determines the order of the grading key printed on the result sheet. |

Seeded scale on installation:

| Grade | Range | Remark | Display order |
| --- | --- | --- | --- |
| A | 80 to 100 | Excellent | 1 |
| B | 70 to 79 | Very Good | 2 |
| C | 60 to 69 | Good | 3 |
| D | 50 to 59 | Fair | 4 |
| E | 40 to 49 | Pass | 5 |
| F | 0 to 39 | Fail | 6 |

#### Save-time validation, in full

Validation runs over the whole submitted scale as one unit. A partially valid scale is never saved. If any rule fails, nothing is written, the editor stays open with the offending row highlighted, and exactly one message is shown, choosing the first failure in the order listed here so that the administrator fixes one thing at a time rather than reading a wall of errors.

| No. | Rule | Rejection message |
| --- | --- | --- |
| 1 | At least one band exists. | The grading scale must contain at least one band. Add a band before saving. |
| 2 | Every bound is a whole number. | Band D has a lower bound of 49.5. Grade bounds must be whole numbers. |
| 3 | Every bound is between 0 and 100 inclusive. | Band A has an upper bound of 105. Grade bounds must be between 0 and 100. |
| 4 | For every band, lower_bound is less than or equal to upper_bound. | Band C has a lower bound of 69 above its upper bound of 60. Swap them or correct the band. |
| 5 | No two bands overlap at any mark. | Band C (60 to 69) overlaps band B (68 to 79) at marks 68 and 69. Bands cannot share a mark. |
| 6 | No mark between the lowest and highest bound is unbanded. | There is a gap between band D (50 to 59) and band C (61 to 69). Mark 60 belongs to no band. |
| 7 | The lowest bound across the scale is 0. | The scale starts at 40. Marks 0 to 39 belong to no band. Extend the lowest band down to 0. |
| 8 | The highest bound across the scale is 100. | The scale ends at 99. Mark 100 belongs to no band. Extend the highest band up to 100. |
| 9 | Grade letters are unique. | Two bands use the grade letter B. Grade letters must be unique. |
| 10 | Every band has a remark of at least three characters. | Band A has no remark. Every band needs a remark, for example Excellent. |

Rules 5, 6, 7 and 8 together are the coverage test: the bands must tile 0 to 100 exactly once with no hole and no double cover. The implementation should sort by lower_bound and walk the list once, which finds every failure in a single pass and lets the message name the specific band and the specific mark at fault. Naming the mark matters. An administrator told there is an overlap will look at six rows and shrug. An administrator told the overlap is at marks 68 and 69 fixes it in four seconds.

#### Live preview and reset

Beside the editor the screen renders the grading key exactly as it will appear in the footer of the result sheet, in the same order and with the same column labels, so the administrator sees the parent's view of what they are editing. Below the preview, a Reset to defaults action requiring `settings.reset.defaults` restores the six seeded bands. The reset confirms first, naming what will be lost, and writes an audit event.

Editing the scale while results are published in the active session triggers the warning in 6.2.9 and requires a reason.

### 6.2.6 Assessment structure

The components are the columns on the score entry sheet and on the result, in the order set here. One row is flagged as the examination and always renders last.

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| name | String 40 | Yes | Unique across components, case-insensitive. For example First CA Test. |
| short_label | String 12 | Yes | Used as the column header where space is tight, for example CA1, EXAM. Unique. |
| max_mark | Integer | Yes | 1 to 100. |
| is_examination | Boolean | Yes | Exactly one component in the structure has this true. |
| display_order | Integer | Yes | System-maintained from the drag order. The examination is forced to last regardless of drag position. |

Seeded structure: First CA Test 15, Second CA Test 15, Assignment 10, Examination 60.

| No. | Rule | Rejection message |
| --- | --- | --- |
| 1 | At least one non-examination component exists. | Add at least one continuous assessment component. A structure of examination only is not supported. |
| 2 | Exactly one component is flagged as the examination. | Exactly one component must be marked as the examination. You have marked two. |
| 3 | Component maximums plus the examination maximum total exactly 100. | The components total 95. You are 5 marks short of 100. Increase a maximum or add a component. |
| 3b | As above, on excess. | The components total 110. You are 10 marks over 100. Reduce a maximum or remove a component. |
| 4 | Every maximum is at least 1. | Assignment has a maximum of 0. Every component must be worth at least 1 mark. |
| 5 | Names and short labels are unique. | Two components are named Second CA Test. Component names must be unique. |

The administrator may collapse the seeded four columns into one CA component of 40 plus Examination 60, split the continuous assessment four ways, or move the school to CA 30 and Examination 70 entirely. All of those are ordinary saves as long as the total is 100.

#### The guard once marks exist

Renaming a component is always safe, including mid-term, because the name is a label and the marks are stored against the component id. Reordering is always safe, because order affects only column position. Changing a maximum or removing a component is not safe, and the rule is deliberately blunt: **once the first mark is entered anywhere in a session, the set of components and their maximums are locked for the whole of that session.**

The reason is comparability. If Second CA Test is worth 15 in First Term and 10 in Second Term, the annual cumulative average is arithmetic nonsense and no explanation on the result sheet will save it. Locking for the session rather than for the term is the only rule that keeps the annual figure meaningful.

Attempted removal or maximum change while locked is rejected with: **Marks have already been entered in 2026/2027. The assessment structure is locked until the session closes. You can still rename or reorder components.** Adding a component is blocked by the same rule, because adding one requires taking marks from another to keep the total at 100.

If the school genuinely needs to change the structure after entering marks in error during a first week of use, the escape route is to void the marks. A Super Admin voids every `subject_score` in the session using `result.score.void` with a reason, at which point the lock lifts. That is intentionally laborious.

### 6.2.7 Trait configuration

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| domain | Enum | Yes | affective or psychomotor. |
| name | String 60 | Yes | Unique within its domain. For example Punctuality. |
| display_order | Integer | Yes | Drag order. Controls the row order in the trait block on the result sheet. |
| status | Enum | Yes | active or archived. Archived traits stay on historical sheets and disappear from new entry screens. |

Trait rating scale, a separate small table:

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| scale_type | Enum | Yes | numeric or letter. Defaults numeric. |
| point_value | String 4 | Yes | For numeric, 1 to 9 as digits. For letter, a single letter. Unique across points. |
| point_label | String 120 | Yes | The legend text for this point, printed once beside the trait block rather than repeated per row, per Appendix C.4. Long enough for a full sentence rather than a single word, because the seeded default is descriptive. |
| point_order | Integer | Yes | Ascending from worst to best. |

Seeded scale, numeric by default per Appendix A entry 48: 5 Maintains an excellent degree of observable traits. 4 Maintains a high level of observable traits. 3 Shows an acceptable level of observable traits. 2 Shows minimal regard for observable traits. 1 Shows no regard for observable traits. A school that prefers single-word labels, or a letter scale, can replace these without any change outside this table: the trait block always prints whatever point_value and point_label the school has configured. The scale must have between 2 and 9 points. Rejection on a single point: **A rating scale needs at least two points.**

Adding a trait mid-term is allowed. It appears immediately on the trait entry screen and the completeness gate in 6.7.4 will then require a rating for it before the arm can be submitted, which is the correct behaviour and should be expected by the administrator who added it. Removing a trait once ratings exist in the active term is rejected: **Ratings have already been entered for Neatness this term. Archive the trait instead, which keeps it on this term's sheets and removes it from next term.** Archiving is the safe path and is what the interface offers first.

### 6.2.8 Result rules

| Field | Type | Req | Validation and default |
| --- | --- | --- | --- |
| annual_method | Enum | Yes | simple_average or weighted. Defaults simple_average. |
| weight_first, weight_second, weight_third | Integer | Cond | Required when annual_method is weighted. Each 0 to 100. Must total exactly 100. Rejection: The three term weights total 90. They must total 100. |
| primary_position_scope | Enum | Yes | arm or level. Defaults arm. Decides which position is printed as Position in Class. |
| show_level_position | Boolean | Yes | Defaults true. When primary_position_scope is arm, this adds a second labelled line showing the position across the whole level. |
| tie_break_rule | Enum | Yes | shared_position, exam_then_ca, or exam_then_alphabetical. Defaults shared_position. |
| pass_mark | Integer | Yes | 0 to 100. Defaults 40. A subject total at or above this is a pass. |
| promotion_threshold | Integer | Yes | 0 to 100. Defaults 40. Annual average at or above this proposes promotion. |
| require_core_pass | Boolean | Yes | Defaults true. When true, promotion also requires a pass in every core subject. |
| core_subject_ids | Array of UUID | Cond | Required when require_core_pass is true. Set by the administrator once subjects exist. The school's expected choice is English Studies and Mathematics. |
| min_subjects_for_position | Integer | Yes | Defaults 1. A pupil with fewer scored subjects than this is excluded from position ranking and the sheet says so. |

Both `primary_position_scope` and `tie_break_rule` are locked once any result set in the session is published, because changing them would make the published Third Term inconsistent with the published First Term inside the same annual computation. `annual_method` and its weights are locked once Third Term is published for any arm. `pass_mark`, `promotion_threshold`, `require_core_pass` and `core_subject_ids` are editable until promotion is run for the session, and are read at the moment a Third Term result set is published, not at the moment promotion runs.

The recommended default is simple average of the three terminal averages. It is what parents expect, it is what the school currently does by hand, and it is defensible when questioned. Weighted annual computation giving Third Term more influence is the alternative, and it is available, but I have made it opt-in because a school that adopts it usually does so without telling parents and the complaints arrive in July. See Appendix A entry 19.

### 6.2.9 Configuration versioning and the binding to published results

Every save to the grading scale, the assessment structure, the trait lists, the trait scale or the result rules writes a new `config_version` row holding the whole serialised configuration. Nothing is overwritten. The row records the actor, the timestamp, the reason if one was required, and a monotonic version number.

Publication writes a snapshot. When `result.publish` succeeds for an arm and term, the system copies the current configuration into `result_set.config_snapshot` and records `config_version_id`. Every subsequent render of that published result, on screen or as a PDF, reads the snapshot. Live settings are read only for result sets that are not yet published.

I recommend the snapshot over version-referencing alone, and the reason is not elegance. Version references are correct until somebody writes a migration that touches the versioned tables, or until a band is edited with a version number reused by mistake, and then thousands of historical grades change quietly and nobody notices for a year. A denormalised copy of a few kilobytes per arm per term cannot be broken by any later edit anywhere in the system. The cost is duplication and the fact that a genuine correction to a grading band does not propagate to already-published results without a deliberate unpublish and republish. That is the right default: propagation should be a decision, not a side effect.

#### What the administrator sees when an edit would affect published results

On opening any settings screen in this group, the system checks whether any result set in the active session is Published. If none is, the screen behaves normally and no warning appears. If any is, the screen shows a persistent notice at the top:

> **7 arms have published results in 2026/2027. Changes you make here will apply to results published from now on. Results already published keep the settings they were published with.**

On save, a confirmation dialogue names the concrete consequence and requires a reason of at least ten characters:

> **You are changing the grading scale. Published results for Primary 1A, Primary 1B, Primary 2A, Primary 2B, Primary 3A, Primary 4A and Primary 5A will not change. Any result set still in Draft, Awaiting Approval or Approved will need to be recomputed before it can be published, and the system will mark those 5 result sets as needing recomputation. Give a reason for this change.**

On save the system sets a `needs_recompute` flag on every result set in the session that is not Published, and those result sets cannot be submitted or approved until computation is run again. This is how the school avoids a term where half the arms were graded on one scale and half on another. The flag is cleared by a successful computation.

### 6.2.10 What can be changed when

| Setting group | Locked by | Detail |
| --- | --- | --- |
| School identity, motto, logo | Never locked | Editable at any time. Published results keep the snapshot values. |
| Abbreviation | Never locked | Editable at any time with confirmation. Issued numbers never change. |
| Registration number width, separator, reset rule | Never locked | Applies to numbers issued after the change. Reducing width is rejected if any issued serial in the current counter exceeds the new width, with the message: Serial 1043 will not fit in a width of 3. Choose 4 or more. |
| Grading scale | Not locked, but guarded | Editable any time. Triggers the 6.2.9 warning and the recompute flag. Published results unaffected. |
| Assessment structure: names and order | Never locked | Safe at any time. |
| Assessment structure: components and maximums | Locked for the session once the first mark is entered | See 6.2.6. |
| Trait lists: add and rename | Never locked | New traits require ratings before submission. |
| Trait lists: remove | Locked once ratings exist in the active term | Archive instead. |
| Trait rating scale | Locked for the session once the first rating is entered | Changing a five-point scale to a three-point scale mid-session would make stored ratings unreadable. |
| Result rules: position scope, tie-break | Locked once anything is published in the session | See 6.2.8. |
| Result rules: annual method and weights | Locked once Third Term is published for any arm | See 6.2.8. |
| Result rules: pass mark, promotion threshold, core subjects | Locked once promotion is run for the session | Read at Third Term publication. |
| Pin defaults | Never locked | Applies to batches generated after the change. Existing pins keep their length and their maximum uses. The maximum-uses default cannot be set below 1 or above 100, and the screen warns above 10 because under unbound pins this figure is the ceiling on what one lost slip discloses, per 6.8.2. |

### 6.2.11 Error and edge cases

| Case | Behaviour |
| --- | --- |
| Two administrators edit the grading scale at once | Optimistic concurrency on the config version. The second save is rejected: The grading scale was changed by another administrator while you were editing. Reload and make your change again. The first save wins and both attempts appear in the audit log. |
| Grading scale saved with bands out of drag order | Accepted. Display order and bound order are independent. The system sorts by lower_bound internally for grade resolution and uses display_order only for printing the key. |
| Reset to defaults while results are published | Allowed, treated as an ordinary edit under 6.2.9, with the same warning and reason requirement. |
| Logo deleted with no replacement | Rejected: A logo is required. Upload a replacement before removing the current one. |
| Head teacher signature missing at publication | Publication is blocked, not the save: A head teacher signature has not been uploaded. Add it in settings before publishing results. |
| Abbreviation changed to a value already used historically | Allowed. Abbreviations are not unique over time and a school returning to a previous prefix is legitimate. |
| Core subject deactivated while require_core_pass is true | Rejected on the subject side: English Studies is a core subject for promotion. Remove it from core subjects in result rules before deactivating it. |

### 6.2.12 Endpoints

| Endpoint | Notes |
| --- | --- |
| GET /settings | Everything in one payload for the settings area. Requires `settings.view`. |
| PATCH /settings/identity | School name, short name, address, phone, email, motto, head teacher name. |
| POST /settings/identity/logo, POST /settings/identity/signature | Multipart image upload. |
| PATCH /settings/abbreviation | Body carries new value, the literal confirmation token and a reason. |
| PATCH /settings/reg-number | Separator, serial width, reset rule. |
| GET /settings/reg-number/preview | Returns the next number that would be issued under supplied unsaved parameters. |
| PUT /settings/grading | Whole scale as one array. Atomic. Returns 422 with the single first failure and the offending band index. |
| POST /settings/grading/reset | Restore seeded bands. |
| PUT /settings/assessment | Whole structure as one array. Atomic. Returns 409 when locked for the session. |
| PUT /settings/traits | Both domains and their order. |
| PUT /settings/trait-scale | Scale points. |
| PATCH /settings/result-rules | Field-level patch, with lock checks per field. |
| PATCH /settings/pin-defaults | Length, maximum uses, character set. Length below 10 is rejected, per 6.8.6. |
| GET /settings/impact | Returns the count and names of published result sets in the active session and the count of result sets that would be flagged for recomputation. The interface calls this to build the warnings in 6.2.9. |
| GET /config-versions, GET /config-versions/{id} | Version history and one full version, for the audit view. |
| GET /setup/checklist | Per-step done, in progress or blocked, with the link target and the blocking reason. |


---

---

## 6.2.13 Amendment: configuration arising from the school's own forms

Added after the school supplied its own result sheets. Each item below **replaces** what 6.2 specified earlier. `25-open-conflicts-to-resolve.md` carries the reasoning and the alternatives; this section states what to build.

### Grading scale: nine bands, not six

The seeded six-band A-to-F scale is replaced by the eight bands both of the school's sheets print, plus a ninth beneath them. The `grade_band` entity and all ten validation rules in 6.2.5 are unchanged; only the seed data changes.

| Grade | Lower | Upper | Descriptor |
|---|---|---|---|
| A+ | 90 | 100 | Very excellent |
| A | 85 | 89 | Excellent |
| B | 75 | 84 | Very good |
| B- | 70 | 74 | Good |
| C+ | 60 | 69 | Average |
| C | 50 | 59 | Fair |
| D | 40 | 49 | More effort |
| E | 20 | 39 | Not Now |
| F | 0 | 19 | Fail |

Nine bands. The eight the school prints are kept exactly as printed, including E at 20-39, and **F 0-19 is added beneath them** to satisfy the contiguity rule in 6.2.5. The descriptor `Fail` is this specification's word, not the school's, since the school's key does not reach that far down. The school can rename it at the grading screen in a few seconds.

The `grade` value is no longer a single character, so the field widens to 3 characters to hold `A+` and `B-`.

One scale serves both sections. Nursery and primary print an identical key.

**This is seed data, not a fixed scale.** Everything above is what the grading screen contains on first run. The school adds, removes, renames and re-bounds bands through the ordinary editor in 6.2.5, subject only to the ten validation rules there, and every change is versioned and snapshotted at publication. No band, boundary or descriptor in this product requires a developer to change.

### Assessment structure: three components, not four

The seeded structure of First CA 15, Second CA 15, Assignment 10, Examination 60 is replaced by:

| Component | Maximum | Is examination | Display order |
|---|---|---|---|
| 1st CA | 20 | No | 1 |
| 2nd CA | 20 | No | 2 |
| Exam | 60 | Yes | 3 |

The assignment component is removed from the seed because neither of the school's sheets has a column for it. The session lock in 6.2.6 still applies and makes this the most time-critical item in this amendment: once a mark is entered anywhere in a session the structure is frozen, so it must be confirmed before scoring begins.

#### Reintroducing an assignment component must stay a configuration change

GRAS does not want an assignment. Another school onboarded onto this product later may, and reintroducing one must be a row in `assessment_component` plus a boundary adjustment, never a code change. That imposes requirements on everything downstream of the component list, and they are stated here because the cheap way to build a three-column sheet is to write three columns.

**Nothing may assume three components, or four, or any number.**

| Surface | Requirement |
|---|---|
| Score entry grid | Columns are generated by iterating the active components in `display_order`. A grid that names its columns is wrong. Two to six non-examination components must all render without horizontal overflow on a laptop. |
| Continuous assessment total | Computed as the sum of every component where `is_examination` is false. The number 40 appears nowhere in code, in a column heading, in a validation message or in a test fixture. |
| Column headings | Composed from the component's own `name` and `max_mark`, so a component renamed to `Assignment` and bounded at 10 produces the heading `Assignment (10)` with no other change. |
| Result sheet PDF, both sections | The subject table's column set is derived per Appendix E.4 and F.2. Column widths are computed from the component count, not fixed. A four-component sheet must still fit A4 portrait at the stated minimum font size. |
| Portal payload | Returns components as an ordered array with name, maximum and score, not as named fields. A client reading `first_ca` and `second_ca` by name breaks the moment a school adds a component. |
| Import template | Component columns are generated into the XLSX from the active component list at download time, and the parser matches on component name rather than column position. |
| Validation | Components must sum to exactly 100 across the session. This is the one invariant that holds regardless of the count, and it is what makes a percentage a percentage. |
| Snapshot | The component list, names and maximums are frozen into the result set at publication, per 6.2.9, so reintroducing a component next session never alters a sheet already issued. |

**Assessment profiles.** To make onboarding a second school a setup action rather than a data-entry session, the seed ships two named profiles, selectable at first run and thereafter through the grading and assessment screen:

| Profile | Components |
|---|---|
| `gras_default` | 1st CA 20, 2nd CA 20, Exam 60 |
| `with_assignment` | 1st CA 15, 2nd CA 15, Assignment 10, Exam 60 |

A profile is a convenience that writes rows into `assessment_component`. It is not a mode, it is not stored on the school, and nothing later branches on which profile was chosen. Selecting one and then editing the rows individually is expected.

Note that a second school means a second installation, per Appendix A entry 1: this product is deliberately not multi-tenant. What these profiles buy is a faster first run for that installation, not tenancy.

### Rating scales become records

6.2.7 specified one school-wide trait scale. The school's two sheets use different scales, so `trait_scale` is replaced by `rating_scale` and `rating_scale_point`, and each rating block references a scale by id.

Three seeded scales:

| Scale | Points | Used by |
|---|---|---|
| Nursery development | E Excellent, S Satisfied, I Improving, N Needs Improvement | The four nursery development domains |
| Primary trait | E Excellent, I Improving, N Needs Improvement | Primary affective and psychomotor blocks |
| Five-point numeric | 5 to 1 with the descriptive legend from Appendix A entry 48 | Nothing. Retained as an alternative. |

`rating_scale_point` keeps the widened `point_label` of 120 characters so a descriptive legend fits, and gains a `point_code` of 1 character holding E, S, I or N as printed in the column heading.

### Development domains and indicators, nursery only

New configuration, seeded from the nursery sheet: four domains, each holding an ordered list of indicators. The full seeded lists are in `21-appendix-e-nursery-result-sheet.md` E.3.

**The school adds, renames, reorders and archives both indicators and whole domains.** The seeded 4 domains and 45 indicators are a starting point, not a fixed structure. Two entities:

| Entity | Fields |
|---|---|
| `development_domain` | id, section, name, display_order, rating_scale_id, allows_indicator_comment, status (active or archived) |
| `development_indicator` | id, domain_id, name, display_order, status |

Rules, following the trait rules already in 6.2.7:

- Adding an indicator mid-term is allowed. It appears immediately on the entry screen and the completeness gate then requires a rating for it before the arm can be submitted, which is correct and should be expected by whoever added it.
- Removing an indicator that has ever been rated is refused in favour of archiving: **Ratings have already been entered for Potty trained this term. Archive the indicator instead, which keeps it on this term's sheets and removes it from next term.**
- Archived indicators and domains stay on historical sheets through the publication snapshot and disappear from new entry screens.
- A new domain may be added with its own rating scale and its own choice of whether indicators carry a comment column, so a school wanting a fifth domain rated on three points rather than four needs no code change.
- There is no cap on indicator count, but the entry screen warns above 60 indicators that a teacher is being asked for more than 1,800 cells per arm, because that is a real cost and the person adding the sixtieth indicator is not the person filling it in.
- Domains and indicators are section-scoped. Nothing stops a school configuring domains for primary as well; GRAS does not, and the primary sheet has no place to print them, so the seed leaves primary with the affective and psychomotor blocks only.

### Trait lists replaced

The affective and psychomotor lists seeded in 6.2.7 were guesses made before the school's form was available. They are replaced by the school's own 11 affective and 8 psychomotor traits, listed in `22-appendix-f-primary-result-sheet.md` F.3.

### Fee notice configuration

New, and the one addition here that touches an area section 3.2 excluded. It is a **printed notice, not a finance module**: no invoicing, no receipts, no part payments, no balances carried forward. See `25-open-conflicts-to-resolve.md` item 5.

| Field | Type | Req | Validation |
|---|---|---|---|
| id | UUID | Yes | System. |
| section | Enum | Yes | nursery or primary. Lets the two sections carry different line labels, since Toiletries and Party Fee are nursery concerns. |
| class_level_id | UUID | No | Null means the amount applies to every level in the section. Set to override for one level. |
| term_id | UUID | Yes | Amounts are per term, because they change. |
| label | String 60 | Yes | Seeded: Tuition Fee, Exam & PTA, Books, Toiletries, Party Fee, Outstanding Fee. |
| amount | Decimal | Yes | Naira, no kobo. Zero is permitted and prints as a dash rather than 0. |
| display_order | Integer | Yes | The order printed. |

The outstanding-fee figure is the only per-pupil value and is stored against the pupil's result set, typed by an administrator or bursar from the school's own records. It is excluded from the parent-facing portal payload by default so that a fee dispute does not travel with a result sheet; the school can switch it on.

#### Entry screen, and what this block deliberately does not do

**Fee lines for a level and term.** A grid: rows are the configured labels, columns are the class levels of the section, cells are amounts. One screen sets the whole section's fees for a term. A `copy from previous term` action fills it from last term's figures, because most lines do not change and retyping nine levels by six lines is how a wrong figure reaches a parent. Adding, renaming, reordering and removing a label is done on the same screen.

**Per-pupil outstanding figure.** A column on the arm's result entry screen beside attendance, and a bulk-entry grid of pupils against one number for a whole arm. Blank is the normal case and prints as a dash.

**No logic is attached to any of it.** Specifically, and these are requirements rather than observations:

- No invoice, no receipt, no payment record, no part payment, no payment method, no reconciliation.
- No balance carried between terms. The outstanding figure for First Term has no relationship to the one for Second Term; each is typed.
- No arithmetic beyond the printed total, which is the sum of the printed lines. Nothing subtracts a payment, nothing computes arrears, nothing ages a debt.
- No pupil is ever blocked from anything by a fee figure. A result is not withheld, a pin is not revoked, a portal lookup is not refused. The temptation to gate results on an outstanding balance should be refused if it is ever raised: it turns a notice into a debt-collection mechanism and puts the system between a child and their marks.
- No report totals fees across pupils except the fee notice audit in `15-reporting-requirements.md` 10.2, which exists only to confirm what was printed on the sheets before they went home.

This is a text block with numbers in it. If the school later wants fee management, that is a separate product decision and a separate module, per `25-open-conflicts-to-resolve.md` item 5.

### Settings that are now section-scoped

For clarity, since this is the pattern a builder needs to hold in mind: **grading scale and assessment structure are school-wide. Rating scales, rated blocks, subject lists and fee lines are section-scoped.** Everything else in 6.2 remains school-wide.
