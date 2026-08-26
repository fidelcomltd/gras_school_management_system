## 6.3 Academic Sessions and Terms

### 6.3.1 Purpose

Nothing in this product exists outside a session and a term. An arm belongs to a session. A subject mapping belongs to a session and a term. A mark belongs to a term. A result set belongs to an arm and a term. If this module is built loosely everything above it inherits the looseness, so it is specified before the modules that depend on it.

### 6.3.2 Actors and required privileges

| Operation | Privilege |
| --- | --- |
| View sessions and terms | `session.view`, held by every role |
| Create a session with its three terms | `session.create` |
| Edit dates, times school opened, resumption date | `session.update` |
| Open a term | `term.open` |
| Close a term | `term.close` |
| Run promotion | `promotion.run` |
| Reverse a promotion batch | `promotion.reverse` |
| Override a pupil's promotion outcome | `promotion.decide` |

### 6.3.3 Entity: academic_session

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| id | UUID | Yes | System. |
| name | String 9 | Yes | Format YYYY/YYYY. The second year must be exactly the first plus one. Rejection: A session runs across two calendar years. 2026/2028 is not valid. Unique. |
| start_date | Date | Yes | Must fall inside the first named year. Must be earlier than end_date. |
| end_date | Date | Yes | Must fall inside the second named year. |
| state | Enum | Yes | upcoming, active, closed. Only one session may be active. |
| promotion_batch_id | UUID | No | Set when promotion has been run for this session. |
| archived_at | Timestamp | No | Set by the end-of-session archive job in 9.8. |

### 6.3.4 Entity: term

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| id | UUID | Yes | System. |
| session_id | UUID | Yes | Exactly three terms per session, created together with the session. |
| ordinal | Integer | Yes | 1, 2 or 3. Immutable. |
| name | String 20 | Yes | First Term, Second Term, Third Term. Editable label, in case the school prefers different wording, but the ordinal drives all logic. |
| start_date | Date | Yes | Inside the session's range. Later than the previous term's end_date. |
| end_date | Date | Yes | Later than start_date. Earlier than the next term's start_date. Overlapping terms are rejected: Second Term starts on 05/01/2027, before First Term ends on 18/12/2026 has passed. Terms cannot overlap. |
| times_school_opened | Integer | Yes to close the term | 1 to 200. Printed on every result sheet for the term and used as the denominator of the attendance block. Can be left blank while the term is upcoming or active, but the term cannot be closed without it and a result set cannot be submitted without it. |
| next_resumption_date | Date | Yes to publish | The date the following term begins. Printed in the result sheet footer as Next Term Begins. For Third Term this is the resumption date of the next session's First Term, which the school usually knows by July. Publication of a result set in this term is blocked until it is filled. |
| state | Enum | Yes | upcoming, active, closed. Only one term across the whole system may be active. |
| closed_at, closed_by | Timestamp, UUID | No | Written on close. |

### 6.3.5 Creating a session

One screen creates the session and all three terms together. There is no route that creates a session without terms, because a session with two terms is not a state the school ever wants and permitting it invites a half-configured year. The form takes the session name, then three rows of start date, end date and next resumption date. Times school opened may be left blank and filled in later, which is realistic: the school does not know in September how many days it will open by December.

On save the session is created in state upcoming with all three terms upcoming. Creating a session does not create arms, does not copy pupils and does not open a term.

A new session can be created while the previous one is still active, which is necessary because the school plans next year in June while Third Term is still running. Only one session may be in state active, and the transition is: the administrator closes the last term of the old session, runs promotion, then opens First Term of the new session, which moves the new session to active and the old session to closed.

### 6.3.6 Term states and what closing does

| State | Behaviour |
| --- | --- |
| upcoming | Visible in lists. No score entry. Subject mappings can be created ahead of time. Arms for the session can be created. Moves to active by `term.open`. |
| active | The only term in which marks can be entered. Exactly one term system-wide is active. Moves to closed by `term.close`. |
| closed | Read-only for marks. Result sets in the term keep their state and published results stay published and readable on the portal forever. Reopening a closed term is possible for a Super Admin only, requires a reason, and is described below. |

Opening a term is blocked unless: the previous term in the same session is closed, or this is the first term of the session and the previous session is closed or has no active term; the session has a start and end date; and at least one arm exists for the session. The block message names the reason, for example: **First Term 2026/2027 cannot be opened because Third Term 2025/2026 is still active. Close it first.**

Closing a term is blocked when any result set in the term is in state Draft, Awaiting Approval or Approved with marks entered. The message lists the offending arms: **These arms have results that are not published: Primary 2B (Awaiting Approval), Primary 5A (Draft). Publish or withdraw them before closing the term.** An arm with no marks entered at all does not block closure, because a nursery arm that the school decided not to score should not hold the term open.

When a term closes: score entry endpoints return 409 for that term; the trait, attendance and remark endpoints do the same; published result sets remain readable and downloadable on the parent portal indefinitely; and the term's `times_school_opened` becomes immutable, because it is printed on results already in parents' hands.

Reopening a closed term requires `term.close` plus `is_super_admin`, a reason of at least ten characters, and is refused outright if the following term has already been opened. The reason this exists at all is the mark discovered in January that should have been in December's result. The reason it is hedged this tightly is that a school which reopens terms casually will end up with two versions of the same result sheet in circulation.

### 6.3.7 End-of-session promotion

Promotion runs once per session, after Third Term is closed, and moves every active pupil into an arm of their level's next level for the new session. It is a bulk operation with per-pupil override, run per level or for the whole school in one action.

#### Preconditions

- Third Term of the session is closed.
- The new session exists, with arms created for every level that will receive pupils. The system checks this first and lists the levels with no arm in the new session: **Primary 3 has no arm in 2027/2028. Create at least one arm before running promotion.**
- Annual cumulative results have been computed for every arm being promoted from, because the promotion proposal reads the annual average.

#### What the system proposes

For each pupil, the system reads the `annual_result` row and applies the rules from settings: annual average at or above `promotion_threshold`, and if `require_core_pass` is true, a pass at or above `pass_mark` in every core subject. It then proposes one of three outcomes.

| Proposed outcome | Meaning |
| --- | --- |
| Promoted | Moves to an arm of the next level in the new session. For a pupil in the terminal level this becomes Graduated instead, the pupil's status changes to graduated, and no new enrolment is created. |
| Repeat | Stays at the same level, enrolled into an arm of that level in the new session. |
| Promoted on trial | Moves up despite falling short. Never proposed by the system, only chosen by a human holding `promotion.decide`, and recorded with a mandatory reason. It exists because head teachers use it and will otherwise record it on paper outside the system. |

#### The review screen and arm assignment

The administrator sees one row per pupil: name, registration number, current arm, annual average, core subject results, proposed outcome, and a target arm selector. Outcome and target arm are both editable per row. Changing an outcome to Promoted on trial reveals a required reason field on that row.

Target arms are pre-filled by a distribution rule that the administrator can change: pupils are spread across the available arms of the destination level in a balanced round-robin by descending annual average, so each arm receives a comparable spread of ability rather than 2A collecting every strong pupil. Where the destination level has one arm, everybody goes there. Where the school wants to stream deliberately, it edits the rows. Streaming by ability is a school decision, not a product opinion, and the default exists only because leaving fifty-four target arm fields blank guarantees mistakes.

The screen shows counts against each destination arm's capacity as the administrator works, and warns rather than blocks when a destination arm is over capacity, matching the rule in 6.4.9.

#### Commit and reversal

Committing writes a `promotion_batch` and one `promotion_decision` per pupil, closes each pupil's current enrolment with an effective-to date of the old session's end date, and opens a new enrolment in the target arm effective from the new session's start date. Pupils marked Graduated have their status changed and no new enrolment. The whole commit runs in one transaction: a failure on pupil forty of two hundred leaves nothing applied.

Reversal requires `promotion.reverse`, held only by a Super Admin, and a reason. It is allowed only while no mark has been entered in the new session and no pin has been used against a pupil in the new session. Otherwise it is refused: **Marks have already been entered in 2027/2028. This promotion cannot be reversed. Move individual pupils between arms instead.** Reversal deletes the new enrolments, reopens the closed ones, restores graduated pupils to active, and marks the batch reversed. The batch row is kept.

Promotion is per pupil in its effects and bulk in its operation. A pupil registered after promotion ran is enrolled directly through the pupil module and is not part of any batch.

### 6.3.8 List and detail views

The session list is short and always will be, so it carries: session name, state, start and end date, number of terms closed, number of arms, number of enrolled pupils, and whether promotion has been run. Default sort is session name descending, newest first. No filters beyond state, because a school will have fewer than twenty rows here for a decade.

The session detail view shows the three terms with their dates, states, times school opened and resumption dates, each editable in place while the term is not closed. Below that it shows the arms created for the session grouped by level in progression order with enrolment counts, and the result publication position for the active term expressed as arms published out of arms total. The promotion panel sits at the bottom and is greyed with an explanatory line until Third Term is closed.

### 6.3.9 Error and edge cases

| Case | Behaviour |
| --- | --- |
| Two terms marked active by a data error | Prevented by a partial unique index across all terms where state is active. The database, not the application, enforces one active term. |
| Session created with overlapping dates against an existing session | Rejected: 2026/2027 overlaps 2025/2026, which ends on 25/07/2026. Sessions cannot overlap. |
| Term closed with times_school_opened blank | Rejected: Enter the number of times school opened for First Term before closing it. This number is printed on every result sheet. |
| Promotion run twice | Blocked. A session carries at most one non-reversed promotion batch: Promotion has already been run for 2026/2027. Reverse the existing batch if you need to run it again. |
| Pupil withdrawn between Third Term publication and promotion | Excluded from the batch. The review screen shows withdrawn pupils in a separate collapsed list so the administrator can see they were not forgotten. |
| Pupil with no annual result at promotion time | Included with proposed outcome blank and a warning icon. The administrator must choose an outcome explicitly. Common for a pupil who joined in Third Term. |
| Terminal level pupil with a failing average | Proposed as Graduated regardless. Nigerian primaries do not hold a Primary 6 pupil back at the end of the year in practice, and the head teacher can override to Repeat if this school does. |
| Reopening a closed term after publication | Allowed under 6.3.6, but the affected result sets stay Published. Editing a mark in a published set is impossible: the set must be withdrawn first, which is itself audited and shows parents a revision notice per 6.7.9. |

### 6.3.10 Endpoints

| Endpoint | Notes |
| --- | --- |
| GET /sessions | List with counts. |
| POST /sessions | Creates the session and its three terms in one transaction. |
| GET /sessions/{id} | Detail with terms, arms grouped by level, publication position. |
| PATCH /sessions/{id} | Name and dates, while upcoming or active. |
| PATCH /terms/{id} | Dates, label, times school opened, next resumption date. |
| POST /terms/{id}/open | Runs the precondition checks and returns the blocking reason on failure. |
| POST /terms/{id}/close | Returns the list of blocking arms on failure. |
| POST /terms/{id}/reopen | Super Admin only, reason required. |
| GET /sessions/{id}/promotion/preview | Returns the proposed rows including target arm suggestions and any pupils without an annual result. |
| POST /sessions/{id}/promotion | Commits the batch. Body carries per-pupil outcome, target arm and reason where required. |
| POST /promotion-batches/{id}/reverse | Reason required. Returns the blocking reason when downstream data exists. |


---
