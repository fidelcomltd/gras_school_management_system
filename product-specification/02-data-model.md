# 5. Data Model

Two structural facts govern this schema and everything else follows from them. First, a class level and an arm are separate entities, and a pupil is enrolled into an arm, never into a level. Second, a published result carries a frozen copy of the configuration that produced it, so that later edits to settings cannot rewrite history.

## 5.1 Entity inventory

| Entity | Lifetime | Purpose |
| --- | --- | --- |
| `admin_account` | Permanent | One row per person who logs in. |
| `role` | Permanent | Named set of privileges. |
| `role_privilege` | Permanent | Join between a role and a privilege string. |
| `role_assignment` | Per session | Joins an account to a role, with scope. Super Admin assignment is sessionless. |
| `role_assignment_arm` | Per session | One row per arm in an arm-scoped assignment. |
| `audit_event` | Permanent, append only | Every write the system performs against a governed entity. |
| `school_profile` | Singleton | School identity, logo, head teacher name and signature, registration number pattern. |
| `grading_band` | Versioned | One band of the grading scale. |
| `assessment_component` | Versioned | One continuous assessment column, plus one row flagged as the examination. |
| `trait` | Versioned | One affective or psychomotor trait. |
| `trait_scale_point` | Versioned | One point on the trait rating scale, with its label. |
| `result_rules` | Versioned singleton | Annual method and weights, position scope, tie-break rule, pass mark, promotion threshold. |
| `pin_defaults` | Singleton | Default pin length, default maximum uses, character set. |
| `config_version` | Append only | One row each time any versioned settings group is saved. Carries the full serialised configuration. |
| `academic_session` | Permanent | For example 2026/2027. |
| `term` | Permanent | Three per session. Carries dates, times school opened, next resumption date, state. |
| `class_level` | Permanent | A year group. Seeded with nine rows, fully editable. |
| `arm` | Per session | A physical class within a level for one session. Nothing seeded. |
| `pupil` | Permanent | One row per child ever admitted or applied for. Registration number lives here, issued at admission approval rather than at record creation, per 6.5.10. |
| `pupil_reg_number_history` | Append only | Superseded registration numbers, kept as lookup aliases. |
| `pupil_contact` | Permanent | Replaces `guardian`. Up to five per pupil, one row per role: father, mother, guardian, emergency_primary, emergency_alternate. Exactly one flagged primary. Per 6.5.5. |
| `admission_record` | Permanent | One per pupil. Form sections A, I and J: application dates, admission type, assessment outcome, the parent declaration and the school's approval. Per 6.5.9. |
| `pupil_health` | Permanent | One per pupil. Form section F. Allergies, medical conditions, medication, dietary instructions, preferred hospital, blood group, genotype. Restricted by `pupil.safeguarding.view`. Per 6.5.7. |
| `authorised_pickup_person` | Permanent | Zero or more per pupil. Form section E, who may collect the child. Per 6.5.6. |
| `barred_person` | Permanent | One per pupil, holding an explicit yes-or-no plus details. Form section E, who may not collect the child. The most restricted data in the system. Per 6.5.6. |
| `pupil_document` | Permanent | One row per checklist document type per pupil, with a received flag, remarks and an optional file. Form section H. Per 6.5.8. |
| `weekly_report` | Permanent | One per pupil per week per term, with a draft or published state. Per 6.10.5. |
| `weekly_report_day` | Permanent | Five per weekly report, Monday to Friday, each holding the eight free-text lines of the weekly sheet. Per 6.10.6. |
| `development_domain`, `development_indicator` | Permanent | Nursery only. The four domains and their rated indicators printed on the nursery sheet. Per Appendix E.3. |
| `development_rating` | Permanent | One per pupil per indicator per term, plus an optional per-indicator comment. Per Appendix E.3. |
| `rating_scale`, `rating_scale_point` | Permanent | Replaces the single school-wide trait scale. Three seeded scales: nursery four-point E/S/I/N, primary three-point E/I/N, and a five-point numeric alternative. Each rating block references one. Per `25-open-conflicts-to-resolve.md` item 6. |
| `fee_schedule_line` | Per session and term | The next-term fee notice printed on both result sheets. Amounts set per class level per term, plus a per-pupil outstanding figure. Display only, not a finance module. Per Appendix E.6. |
| `enrolment` | Per session | Dated membership of a pupil in an arm. |
| `subject` | Permanent | For example Mathematics. |
| `subject_mapping` | Per session and term | Subject applies to a level for one term. |
| `subject_mapping_exception` | Per session and term | Subject included in or excluded from one arm against the level default. |
| `result_set` | Per session and term | One row per arm per term. Holds the state machine and the configuration snapshot. |
| `subject_score` | Per session and term | Raw marks. One row per pupil per subject per term, with one value per assessment component. |
| `subject_result_line` | Computed | One row per pupil per subject per term. Total, grade, remark, subject position. |
| `subject_arm_statistic` | Computed | One row per subject per arm per term. Highest, lowest, class average, number scored. |
| `pupil_term_result` | Computed | One row per pupil per term. Totals, average, arm position, level position, remarks, promotion status. |
| `trait_rating` | Per session and term | One row per pupil per trait per term. |
| `attendance_entry` | Per session and term | Times present and times absent per pupil per term. |
| `annual_result` | Per session | One row per pupil per session. Cumulative average, annual position, promotion outcome. |
| `pin_batch` | Permanent | A generated run of pins. Carries the maximum uses that every pin in it inherits. |
| `pin` | Permanent | One pin, hashed. Not bound to any pupil, per 6.8.2. |
| `pin_use` | Append only | One row per viewing session opened with a pin, recording which pupil was opened. |
| `portal_attempt` | Rolling, purged | Failed and successful lookup attempts, for rate limiting. |
| `promotion_batch` | Permanent | A run of end-of-session promotion, reversible while nothing downstream has changed. |
| `promotion_decision` | Permanent | Per pupil outcome inside a batch. |

## 5.2 The level to arm to pupil chain

A `class_level` row is a year group with a name, a section, a progression order and a pointer to the level that follows it. Primary 2 is one row. It has no pupils.

An `arm` row belongs to exactly one `class_level` and exactly one `academic_session`. Primary 2A for 2026/2027 is one row. Primary 2A for 2027/2028 is a different row. The arm holds the label, the capacity, the form teacher and the status. It does not hold a display name as text: the display name is composed at read time from the level name and the label by the rule in section 6.4.3.

An `enrolment` row joins one `pupil` to one `arm`, with an effective-from date and an optional effective-to date. A pupil has exactly one open enrolment at any moment. Moving a pupil from Primary 2A to Primary 2C closes the first enrolment on the transfer date and opens a second. The history of who sat where, and from when, is therefore recoverable, which matters when a parent disputes a position computed for an arm their child only joined in week six.

A `pupil` row does not carry a level or an arm column. Asking which arm a pupil is in is a query against `enrolment` for the open row. This is the single most important normalisation in the schema and shortcutting it with a `pupil.arm_id` column will break transfers, promotion and historical results at the same time.

| Relationship | Cardinality | Notes |
| --- | --- | --- |
| `class_level` to `arm` | 1 to many | A level may have zero arms in a session, or six. Zero is normal for a deactivated level. |
| `academic_session` to `arm` | 1 to many | Arms are created per session. See the decision in Appendix A entry 14. |
| `arm` to `enrolment` | 1 to many | Capacity is checked against the count of open enrolments. |
| `pupil` to `enrolment` | 1 to many | Exactly one open at a time. Enforced by a partial unique index on (pupil_id) where effective_to is null. |
| `class_level` to `class_level` | 1 to 0..1 | The `next_level_id` self reference forms the progression chain. |
| `pupil` to `pupil_contact` | 1 to 0..5 | At most one row per role. At least one of father, mother or guardian is required for approval, as is emergency_primary. Exactly one row is the primary contact. |
| `pupil` to `admission_record` | 1 to 1 | Created with the pending pupil in step 1 of the admission flow. |
| `pupil` to `pupil_health` | 1 to 1 | Created empty with the pending pupil. Its three yes-or-no questions must be answered explicitly before approval. |
| `pupil` to `authorised_pickup_person` | 1 to many | Ordered as the parent listed them. |
| `pupil` to `barred_person` | 1 to 1 | Always exists, because the explicit No is itself an answer worth storing. |
| `pupil` to `pupil_document` | 1 to many | One row per configured document type, created as a set. |
| `pupil` to `weekly_report` | 1 to many | One per week of each term the pupil was enrolled for. Unique on (pupil_id, term_id, week_number). |
| `weekly_report` to `weekly_report_day` | 1 to 5 | Created together so the entry grid always has its cells. |
| `pupil` to `development_rating` | 1 to many | Nursery pupils only. One row per indicator per term. |
| `class_level` to `fee_schedule_line` | 1 to many | Fee amounts are set per level per term. Only the outstanding-fee figure is per pupil. |
| `class_level` to `subject_mapping` | 1 to many | Mapping is per level per session per term. |
| `arm` to `subject_mapping_exception` | 1 to many | Include or exclude a subject for this arm only. |
| `arm` to `result_set` | 1 to 3 per session | One result set per term. |
| `result_set` to `subject_score` | 1 to many | Marks belong to the result set for the arm the pupil was in when the term's results were computed. |
| `pupil` to `pin` | None | There is no relationship. A pin names no pupil and a pupil holds no pin, per 6.8.2. |
| `pin` to `pin_use` | 1 to many | Bounded by the pin's maximum uses. |
| `pupil` to `pin_use` | 1 to many | Every viewing session records the pupil it opened. This is the only link between a pin and a child, and it is created by use rather than by generation. |

## 5.3 Settings to result binding

Configuration is versioned, and a published result carries a snapshot. Both mechanisms exist, and each does a different job.

- `config_version` is written every time a versioned settings group is saved. It stores the full serialised state of the grading scale, the assessment structure, the trait lists, the trait scale and the result rules, together with who saved it and when. Nothing is updated in place. This gives the audit log something concrete to point at and lets the school see what the scale looked like in March.
- `result_set.config_snapshot` is a JSON column written once, at the moment `result.publish` succeeds. It holds a copy of the same structure, taken from the then-current `config_version`, plus the school name, short name and head teacher name at that moment, plus the composed display name of the arm and the level name. `result_set.config_version_id` records which version it came from.
- Every read of a published result renders from the snapshot, never from live settings. Every read of an unpublished result renders from live settings, because an unpublished result is still being worked on and should reflect the school's current rules.

The consequence is that a grading band edited in March changes nothing about December's published results, and the parent who downloads a First Term PDF in June sees the same grades their neighbour saw in December. The trade-off is duplication: the snapshot for a thirty-pupil arm with nine subjects is a few kilobytes, and there is one per arm per term, so a school running twelve arms across three terms writes thirty-six snapshots a session. That is nothing. Section 6.2.9 specifies what the admin sees when an edit would affect published results.

## 5.4 Written entity relationship description

Read from the top. An `academic_session` owns three `term` rows and every `arm` created for that year. Each `arm` points at one `class_level`, which sits in a chain with its neighbours through `next_level_id`. Pupils reach an arm through `enrolment`, which is dated, so the roster of Primary 2A on 12/01/2027 is answerable.

Subjects reach an arm indirectly. A `subject_mapping` attaches a subject to a level for a named session and term. Every arm under that level inherits the mapping. A `subject_mapping_exception` on a specific arm either adds a subject the level does not take or removes one it does. The set of subjects in effect for an arm in a term is the level mappings for that term, plus that arm's include exceptions, minus that arm's exclude exceptions. That set defines the rows of every result sheet produced for that arm that term.

Marks live in `subject_score`, one row per pupil per subject per term, with a JSON map from `assessment_component.id` to an integer mark, plus an examination mark and an `exam_absent` flag. The row belongs to a `result_set`, and the `result_set` is the unit of work: it is the thing that is Draft, submitted, approved, published or withdrawn, and it covers one arm for one term.

Computation reads `subject_score` and writes three computed tables. `subject_result_line` holds one row per pupil per subject: continuous assessment total, examination mark, subject total, grade letter, remark, and subject position within the arm. `subject_arm_statistic` holds one row per subject per arm: highest, lowest, class average and the number of pupils counted. `pupil_term_result` holds one row per pupil: total obtainable, total obtained, average, arm position, optional level position, number of pupils in the arm, the two remarks, and at Third Term the promotion status. Computed rows are deleted and rewritten wholesale on every recomputation, which keeps the code simple and means a recomputation cannot leave half a stale sheet behind.

`annual_result` reads the three `pupil_term_result` rows for a pupil in a session and writes one cumulative row. It is the only entity that spans terms.

On the access side, a `pin_batch` is generated with a pin count and a maximum-uses value, and produces that many `pin` rows, each storing a hash and no pupil reference at all. A pin becomes associated with a child only when it is used: `pin_use` records each viewing session opened, naming the pin and the pupil it opened, and that table is the whole of the audit trail connecting the two. `portal_attempt` records every lookup attempt including failures, keyed by registration number, pin prefix and truncated source address, and is purged on the schedule in section 9.9.

`audit_event` sits beside all of it with a polymorphic reference: entity type, entity id, action, actor, timestamp, and a JSON diff of before and after. It is written by the same transaction as the change it records, so an audit gap means a failed transaction, not a lost event.


---


## 5.5 Amendments arising from the school's own forms

Four structural changes were made after the school supplied its admission form and its two result sheets. They are listed here because they change the shape of the model rather than adding to it, and `25-open-conflicts-to-resolve.md` carries the reasoning.

1. **`guardian` becomes `pupil_contact` with a role.** Two guardian slots cannot hold the form's five contact slots. Existing references to `guardian.view` are aliased to `contact.view` rather than broken.
2. **Health data moves off `pupil` into `pupil_health`.** It was three optional columns; it is now nine fields including three required yes-or-no answers, and it sits behind its own privilege with audited reads.
3. **A pupil is born pending.** `registration_number` is nullable until admission approval, and every roster, count, score sheet and portal lookup filters pending out. This is the single most invasive change, because almost every query touching pupils now needs the status filter.
4. **Rating scales become records rather than one setting.** Nursery rates four points with per-indicator comments, primary rates three points with none. A single school-wide scale cannot serve both sheets.
