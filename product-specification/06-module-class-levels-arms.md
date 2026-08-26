## 6.4 Class Levels, Arms and Classroom Management

### 6.4.1 Purpose and the separation that matters

Primary 2 is a year group. Primary 2A is a room with twenty-eight children in it. These are two entities and they behave differently: the year group persists across sessions and sits in a progression chain, while the room is created for one session, has a capacity and a form teacher, and is the cohort a position in class is computed within. Every result operation in this product names an arm. None of them names a level.

### 6.4.2 Class level

Levels are admin-managed records with full create, read, update and delete. They are seeded with nine rows so the school does not face a blank screen, and there is nothing in code that assumes those nine or their names. A school that wants a Reception year between Nursery 3 and Primary 1 adds it through this screen and the progression chain rewires around it.

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| id | UUID | Yes | System. |
| name | String 40 | Yes | Unique, case-insensitive, trimmed. 2 to 40 characters. Rejection on collision: A class level named Primary 3 already exists. |
| section | FK | Yes | References the section list, itself admin-editable. Seeded with Nursery and Primary. |
| progression_order | Integer | Yes | 1 upward. Unique across active levels. Maintained by the drag order on the list screen, and editable directly for the administrator who prefers typing. |
| next_level_id | UUID | No | Null on exactly one level, the graduating level. Must not equal id. Must reference an active level when this level is active. |
| is_entry_level | Derived | n/a | Not stored. Computed as the active level that no other active level points at. |
| status | Enum | Yes | active or inactive. Defaults active. |
| created_at, updated_at | Timestamp | Yes | System. |

Seeded rows: Nursery 1, Nursery 2, Nursery 3 in section Nursery, then Primary 1 to Primary 6 in section Primary, chained in that order, with Primary 6 as the graduating level. The chain crosses the section boundary at Nursery 3 to Primary 1. Progression is not scoped within a section and no code should assume it is.

#### Progression chain integrity

Validation runs over active levels only, as one set, on every save that touches a name, an order, a next level or a status. Inactive levels keep their stored `next_level_id` for historical reference and are excluded from every rule below. A save that breaks any rule is rejected whole, with one message naming the rule and the offending levels.

| No. | Rule | Rejection message |
| --- | --- | --- |
| 1 | No level points at itself. | Primary 4 cannot be its own next level. |
| 2 | No cycle exists anywhere in the chain. | Primary 3 leads to Primary 4, which leads back to Primary 3. The progression chain cannot loop. |
| 3 | Exactly one active level is pointed at by no other active level. That is the entry level. | Two levels have nothing leading into them: Nursery 1 and Reception. Exactly one level can be the entry level. Point one of them at from another level, or make it follow one. |
| 3b | As above, when none exists. | Every level is pointed at by another, so there is no entry level. Clear the next level on whichever level should be last, or remove a pointer into the level that should be first. |
| 4 | Exactly one active level has a null next level. That is the graduating level. | Two levels have no next level: Nursery 3 and Primary 6. Exactly one level can be the graduating level. Point Nursery 3 at the level that follows it. |
| 4b | As above, when none exists. | Every level has a next level, so there is no graduating level. Clear the next level on the final level. |
| 5 | Every active level is reachable from the entry level by following next_level_id. | Reception cannot be reached from Nursery 1 by following the chain. Every level must be reachable from the entry level. |
| 6 | An active level's next_level_id references an active level. | Primary 3 points at Reception, which is inactive. Point Primary 3 at an active level, or reactivate Reception. |
| 7 | progression_order is unique across active levels. | Primary 2 and Primary 3 both have progression order 5. Each level needs its own position. |
| 8 | progression_order agrees with the chain direction. | Primary 4 has progression order 6 but follows Primary 5, which has order 8. Reorder the levels so the numbering matches the chain. |

Rule 8 exists because two independent representations of the same ordering will diverge, and when they do, the list screen sorts one way while promotion moves pupils another. The interface avoids the problem in practice by rewriting `progression_order` from the drag order and inferring `next_level_id` from adjacency, so the administrator inserting Reception between Nursery 3 and Primary 1 drags one row and the system writes both fields. Rule 8 is the backstop for the direct-edit path and for imports.

#### Inserting a level: the worked case

The school adds Reception between Nursery 3 and Primary 1. The administrator clicks Add level, enters Reception, picks section Nursery, and chooses Insert after Nursery 3. The system then, in one transaction: creates Reception with progression order 4, sets Nursery 3's next level to Reception, sets Reception's next level to Primary 1, and shifts progression order on Primary 1 to Primary 6 up by one. It reruns the eight rules over the result before committing. The nine-row chain becomes ten rows and nothing else in the system needs touching, because arms point at levels by id.

#### Deactivation

Deactivating a level does four things and no more. It hides the level from new arm creation. It hides it from new enrolment and from the promotion target selector. It removes it from the active chain, so the eight rules are rerun without it and the entry or graduating level may shift as a result. It leaves every existing arm, enrolment, mark and published result exactly as they are, and those remain readable and printable forever.

A school that runs no nursery deactivates Nursery 1, Nursery 2 and Nursery 3. After the third deactivation, no active level points at Primary 1, so Primary 1 becomes the entry level and rule 3 is satisfied. The system does not require the administrator to clear Nursery 3's next level pointer: it stays, harmlessly, pointing at Primary 1, and comes back correctly if the nursery is reactivated. Deactivation is rejected only if it would break a rule among the remaining active levels, for example deactivating Primary 3 out of the middle of the chain, which is refused with: **Deactivating Primary 3 would leave Primary 4 unreachable. Point Primary 2 at Primary 4 first, then deactivate Primary 3.**

#### Delete against deactivate

One rule, stated once and enforced on every entity in this product: **delete is permitted only where nothing has ever referenced the row. Everything else deactivates.** For a class level, nothing has ever referenced it means no arm has ever been created under it, no enrolment has ever pointed at an arm under it, and no subject mapping and no result has ever named it. Where any of those exist, the delete button is absent and the interface offers Deactivate with a line explaining why: **Primary 3 has 2 arms and 47 enrolments in its history and cannot be deleted. Deactivate it instead, which keeps its results readable.**

#### Renaming a level after results are published

The published result shows the name captured at publication, held in the result set's configuration snapshot. This is consistent with the settings snapshot decision in 6.2.9, and it is the only answer that does not rewrite a document a parent already holds. Renaming Primary 1 to Basic 1 in 2028 leaves the 2026 result sheets reading Primary 1A and makes the 2028 sheets read Basic 1A. The rename screen says so before saving: **Results already published keep the name Primary 1. New results will use Basic 1.**

### 6.4.3 Arm

Nothing is seeded. The administrator creates arms per level, on demand, in whatever quantity that level needs. Three arms of Primary 1 alongside one arm of Primary 6 is an ordinary configuration and the interface does not treat it as unusual.

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| id | UUID | Yes | System. |
| class_level_id | UUID | Yes | Must reference an active level at creation time. |
| session_id | UUID | Yes | Must reference a session that is upcoming or active. |
| label | String 16 | Yes | Letters, digits and single internal spaces. Trimmed. Unique within the same level and session, case-insensitive. Rejection: Primary 1 already has an arm labelled A in 2026/2027. |
| display_name | Derived | n/a | Never stored as text. Composed by the rule below. |
| capacity | Integer | Yes | 1 to 100. Defaults 30. |
| form_teacher_admin_id | UUID | No | Must reference an active admin account. Not required at creation, required before a result set for the arm can be submitted. |
| status | Enum | Yes | active, inactive or closed. Defaults active. closed is set automatically when the session closes. |
| created_at, updated_at | Timestamp | Yes | System. |

#### Display name composition, stated once

`display_name` is composed at read time and never stored:

> **If the label is a single alphanumeric character, the display name is the level name followed immediately by the label with no space. Otherwise the display name is the level name, one space, then the label.**

So Primary 1 plus A renders as Primary 1A. Primary 1 plus Gold renders as Primary 1 Gold. Nursery 2 plus B renders as Nursery 2B. This one function is used everywhere without exception: the arm list, the arm selector, the score entry sheet header, the roster, the pupil detail view, the pin slip, the parent portal, the result PDF, every report and every CSV export. There is no second implementation and no place where the administrator types a display name by hand, because the moment there is, the label and the display name drift and a parent receives a result sheet reading Primary 1 A.

A level with a single arm still displays as level plus label. Primary 4 with one arm labelled A is Primary 4A everywhere. The alternative, showing bare Primary 4 when there is only one arm, was rejected: it means the display name changes the day the school adds a second arm, so results printed in October read Primary 4 and results printed in January read Primary 4A for the same children, and it forces conditional logic into the one function that must never be conditional. See Appendix A entry 12.

#### Arm creation flow

1. The administrator picks a level from the active levels for the current session.
2. The system suggests the next unused label in sequence: A if the level has no arm this session, B if it has A, C if it has A and B. The suggestion is pre-filled and editable, because some schools label arms by colour or by the form teacher's name.
3. Capacity defaults to 30 and to the capacity of the most recently created arm in the same level if one exists, which is usually right.
4. Form teacher is optional at this point and is a searchable selector over active admin accounts.
5. Save creates the arm. If the level has an active subject mapping for the current term, the new arm inherits it automatically with no action from the administrator, because mappings attach to the level.

Bulk creation exists for the start of a session, where an administrator opening 2027/2028 does not want nine separate forms. One action, Create arms for session, lists every active level with a number-of-arms field defaulting to the count that level had last session, and a capacity field defaulting to last session's capacity. The administrator adjusts the numbers, sees a preview listing the twelve arms that will be created with their composed display names, and commits in one transaction. Labels are assigned A upward. Levels set to zero arms are skipped, which is how a school that has no Primary 6 intake this year handles it.

Arms do not carry over automatically when a session opens. Bulk creation is the deliberate act that brings them into the new session, and it defaults to last session's shape so the common case is three clicks. The reason for not carrying them over silently is in 6.4.7.

### 6.4.4 Adding an arm mid-session

This is the flow the whole design exists for, so it is specified in full. Week six of First Term, Primary 2 has 2A with thirty-one pupils and 2B with thirty, both over capacity, and the school has hired a teacher and opened a third room.

1. The administrator creates Primary 2C under Primary 2 for the current session, capacity 22, form teacher the new hire. Creation is permitted while the term is active with no special privilege beyond `arm.create`.
2. Primary 2C inherits Primary 2's subject mappings for the current term automatically. Per-arm exceptions belonging to 2A or 2B are not copied, because an exception is a statement about one room, not about the level. If 2A takes French as an include exception and the school wants 2C to take it too, the administrator adds the exception to 2C explicitly.
3. The administrator opens the bulk transfer screen, selects pupils from 2A and 2B, sets the destination to 2C, enters an effective date defaulting to today, and commits. Each transfer closes the pupil's open enrolment with effective_to set to the day before, and opens a new enrolment in 2C from the effective date.
4. Marks already entered travel with the pupil. A `subject_score` row is keyed to the pupil, the subject and the term, not to the arm. The mark of 13 that Adaeze scored in her First CA Test in Primary 2A remains her mark of 13 after she moves to Primary 2C. Nothing is retyped and nothing is lost.
5. Any result set for 2A or 2B in this term that had already been computed is flagged `needs_recompute`, because its cohort has changed and its class averages, highest and lowest scores and positions are now wrong. Any such result set in state Awaiting Approval is returned to Draft automatically with the system-written note Cohort changed by pupil transfer on 14/10/2026.
6. A result set for 2A or 2B in this term that is already Published blocks the transfer. The message is: Primary 2A results for First Term are published. Withdraw them before moving pupils out of the arm. The administrator must make a deliberate choice to withdraw, which triggers the revision notice in 6.7.9, rather than have the system silently invalidate a document parents already hold.
7. A result set for Primary 2C is created on first score entry, in Draft, containing the pupils enrolled in 2C.

#### How position computation treats a cohort split part way through the term

Position, class average, highest and lowest are computed over the pupils whose open enrolment is in the arm at the moment computation runs. A pupil who spent five weeks in 2A and seven in 2C is ranked in 2C, against the pupils in 2C, using the marks they earned across both rooms. There is no proportional splitting and no dual ranking.

This is the right answer because the result sheet says Primary 2C at the top and a parent reading position 4 of 22 will check it against the twenty-two names they know. It is also imperfect, and the imperfection should be understood rather than hidden: a pupil transferred late has been ranked against a cohort they did not sit their first CA test with. Where a transfer happens after the examination has been written, the head teacher should hold the pupil in their original arm for that term's result and move them at the term boundary instead. The transfer screen says so: **Moving a pupil after the examination has been marked will rank them against a class they did not sit the term with. Consider transferring at the end of term.** It is a warning, not a block, because the school knows its own circumstances.

### 6.4.5 Arm list view

The list has to answer three questions an administrator actually asks: how full is each room, who teaches it, and are its results in. Columns were chosen against those, and everything else was pushed to the detail view.

| Column | Why it is here | Sortable |
| --- | --- | --- |
| Class | The composed display name, for example Primary 2C. The only identifier anybody uses out loud. | Yes, by chain order |
| Level | Needed for grouping and for the reader scanning a long list. Shown as a subtle grouping header rather than a repeated cell. | Yes |
| Enrolled of capacity | Rendered as 31 / 30 with the number in red when over. This single column answers the fullness question and replaces three separate columns for capacity, enrolled and remaining. | Yes, by enrolled |
| Form teacher | Blank cells here are the reason a result set cannot be submitted, so they need to be visible. | Yes |
| Subjects mapped | A count. A zero here in week two of term is a problem, and it is invisible unless the list shows it. | Yes |
| Result status, active term | The result set state for the active term, or Not started. This is the column the head teacher reads at the end of term. | Yes |
| Status | Active, Inactive or Closed. Only meaningful when the session filter includes closed sessions. | Yes |

Fields that live only in the detail view: arm label on its own, capacity as a bare number, created date and creator, the pupil roster, the subject mapping detail, per-term result history, and the transfer history in and out of the arm. Session is not a column because the list is filtered to one session at a time and the session appears in the filter bar.

Filters: level, session, arm label, status, result status for the active term, and form teacher. Free-text search matches the composed display name, so typing 2c finds Primary 2C.

Default sort groups arms under their level in progression order, then by label ascending within the level. Not alphabetical. Alphabetical puts Nursery 1 before Primary 1 by accident and Primary 10 before Primary 2 on purpose, and a head teacher scanning for Primary 5 should find it between Primary 4 and Primary 6. The sort key is the level's `progression_order` followed by the arm label collated naturally.

The detail view shows: the arm header with composed display name, level, session, capacity, enrolled count and form teacher; the pupil roster as a table of photograph thumbnail, registration number, name, sex, age and status, each row routing to the pupil detail view; the subjects in effect for the arm this term, with level-inherited and arm-exception rows visually distinguished; the result set state for each term of the session with a route into the score sheet; and the transfer log for the arm.

### 6.4.6 Capacity enforcement

Capacity is a soft limit. Enrolling a pupil into a full arm shows: **Primary 2A is at its capacity of 30. Enrol anyway?** An administrator holding `arm.capacity.override` may proceed, and the override is written to the audit log with the arm, the pupil and the resulting count. An administrator without the privilege sees the same message without the proceed button and is told to raise the capacity or choose another arm.

Hard blocking was rejected. In practice the proprietor admits a pupil in week three whatever the room holds, and a system that refuses will be worked around by editing the capacity to 45, which destroys the number's meaning. A soft limit with an audited override keeps the capacity honest and keeps the override visible.

### 6.4.7 Settled rules

| Question | Decision |
| --- | --- |
| Is an arm a per-session record or a permanent record with pupils rotating through it? | Per session. Primary 2A for 2026/2027 and Primary 2A for 2027/2028 are different rows. The justification is that everything an arm carries changes annually: the roster entirely, the form teacher usually, the capacity often. A permanent arm would need every one of those attributes moved into a per-session join table, at which point the join table is the arm and the permanent row holds only a label. Per-session records also make a result set's reference to an arm unambiguous, and make the arm list for a closed session a faithful record of that year rather than a set of rows mutated since. See Appendix A entry 14. |
| Can an arm be deleted once pupils are enrolled? | No. Delete is available only where no enrolment has ever existed, per the single rule in 6.4.2. Otherwise the arm is deactivated, which hides it from new enrolment while keeping its roster and results readable. |
| What happens to an arm when its session closes? | Status moves to closed automatically as part of closing the session. The arm becomes read-only: no enrolment, no transfer, no mark entry, no form teacher change. Its published results remain on the parent portal indefinitely. |
| Do arms carry over when a new session opens? | No, they are created fresh, with bulk creation defaulting to last session's shape. Automatic carry-over would create arms for levels the school has stopped running and would silently carry a capacity and a form teacher nobody reviewed. Three clicks with a preview is better than a surprise. |
| Transferring a pupil between arms mid-term: do entered marks move? | Yes. Marks are keyed to pupil, subject and term. See 6.4.4 step 4. |
| How does position computation handle a pupil who spent half a term in each arm? | Ranked in the arm holding their open enrolment when computation runs, against that arm's pupils, using all marks earned in the term. See 6.4.4. |
| May two arms of the same level run different subject sets? | Yes, through per-arm exceptions in 6.6.4. The mapping screen shows a warning listing the difference, because unintended divergence between 2A and 2B produces result sheets with different rows and parents who compare them. |
| May an arm exist under an inactive level? | Existing arms yes, new arms no. The level selector on arm creation lists active levels only. |

### 6.4.8 Error and edge cases

| Case | Behaviour |
| --- | --- |
| Duplicate label within the same level and session | Rejected by a unique index on (class_level_id, session_id, lower(label)) as well as by application validation. |
| Same label in a different level | Allowed. Primary 1A and Primary 2A coexist. |
| Same label in the same level in a different session | Allowed and expected. |
| Label entered as lowercase b | Normalised to uppercase on save when the label is a single letter. Multi-character labels keep the case as typed, so Gold stays Gold. |
| Arm created for a session that is already closed | Rejected: 2025/2026 is closed. Arms can only be created in an upcoming or active session. |
| Form teacher assigned to two arms | Allowed. One teacher covering two arms happens, and the Class Teacher assignment carries both. |
| Form teacher's account deactivated mid-term | The arm keeps the reference and the list shows the name with a warning icon. Result submission is blocked until a form teacher with an active account is assigned: Primary 3B has no active form teacher. Assign one before submitting results. |
| Capacity reduced below current enrolment | Allowed with a warning: Primary 2A currently holds 31 pupils. Setting capacity to 25 will show this arm as over capacity. No pupil is removed. |
| Bulk arm creation run twice | Labels continue from the highest existing label, so a second run over a level that already has A and B creates C. The preview makes this visible before commit. |
| Level deactivated while arms under it are active | Allowed if the chain rules survive. The arms remain active for the current session and are excluded from next session's bulk creation defaults. |
| Two administrators create the same label simultaneously | The unique index rejects the second insert and the interface retries with the next suggested label, showing: Label A was just taken. Using B. |

### 6.4.9 Endpoints

| Endpoint | Notes |
| --- | --- |
| GET /levels | Active by default, all with a flag. Returns chain order, entry and graduating flags, arm counts. |
| POST /levels | Body may carry insert_after_level_id, in which case the server rewires the chain and reorders in one transaction. |
| PATCH /levels/{id} | Name, section, next level, order, status. Reruns the eight chain rules and returns 422 with the single failure. |
| POST /levels/reorder | Whole ordered array of level ids. Atomic. The interface uses this for drag and drop. |
| DELETE /levels/{id} | Permitted only when nothing has ever referenced the level. Returns 409 naming the references otherwise. |
| GET /sections, POST /sections, PATCH /sections/{id} | The section list, admin-editable. |
| GET /arms | Filtered by session, level, status, result status, form teacher. Returns composed display names. |
| GET /arms/next-label?level_id=&session_id= | The suggested next label. |
| POST /arms | Single arm creation. |
| POST /arms/bulk | Body carries session and an array of level id plus arm count plus capacity. Supports a dry_run flag returning the preview. |
| GET /arms/{id} | Detail with roster, subjects in effect, result set states, transfer log. |
| PATCH /arms/{id} | Label, capacity, form teacher, status. |
| DELETE /arms/{id} | Permitted only where no enrolment has ever existed. |
| POST /arms/{id}/transfer-in | Bulk transfer. Body carries pupil ids and an effective date. Returns the recompute and publication consequences before committing when dry_run is set. |


---
