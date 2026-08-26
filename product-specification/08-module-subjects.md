# Module 6.6: Subject Management

## 6.6 Subject Management

### 6.6.1 Purpose

The subjects in effect for an arm in a term are the rows of every result sheet produced for that arm that term. Nothing else in the product decides those rows.

### 6.6.2 Entity: subject

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| id | UUID | Yes | System. |
| name | String 80 | Yes | Unique, case-insensitive. For example English Studies. |
| code | String 12 | Yes | Uppercase letters and digits. Unique. For example ENG, MTH, BST. Used as the column header on the broadsheet where the full name will not fit. |
| description | String 300 | No | Free text, for the administrator's benefit only. Never printed. |
| status | Enum | Yes | active or inactive. Defaults active. An inactive subject cannot be newly mapped and stays on existing mappings until they end. |

No subjects are seeded. A wrong seeded subject list is worse than an empty one, because the administrator edits around it rather than deleting it, and the school ends up with both Basic Science and Basic Science and Technology in the register. The template on the subject creation screen offers the common Nigerian primary list as suggestions the administrator can click to prefill the name and code: English Studies, Mathematics, Basic Science and Technology, National Values Education, Cultural and Creative Arts, Nigerian Language, Christian Religious Studies, Islamic Religious Studies, Physical and Health Education, Computer Studies, Agricultural Science, Handwriting, Verbal Reasoning, Quantitative Reasoning. Clicking a suggestion is one action that creates one subject. Nothing is created without a click.

### 6.6.3 Entity: subject_mapping

A mapping attaches a subject to a class level for one session and one term. Every arm under that level inherits it.

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| id | UUID | Yes | System. |
| subject_id | UUID | Yes | Must reference an active subject at creation. |
| class_level_id | UUID | Yes | Must reference an active level. |
| session_id | UUID | Yes | Must be upcoming or active. |
| term_id | UUID | Yes | Must belong to session_id. |
| display_order | Integer | Yes | Row order of the subject on the result sheet for that level. Drag-ordered. |
| status | Enum | Yes | active or ended. |

Unique on (subject_id, class_level_id, term_id) where status is active. Mapping is per term rather than per session so that a subject introduced in Second Term does not appear as an empty row on the First Term sheet, and a subject dropped after First Term leaves that term's published result intact. The copy action described below makes the per-term granularity cheap to work with.

### 6.6.4 Entity: subject_mapping_exception

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| id | UUID | Yes | System. |
| arm_id | UUID | Yes | The one arm the exception applies to. |
| subject_id | UUID | Yes | Active subject. |
| term_id | UUID | Yes | Must match the arm's session. |
| mode | Enum | Yes | include or exclude. include adds a subject the level does not take. exclude removes one the level does take. |
| reason | String 200 | Yes | Required. An exception without a stated reason becomes a mystery within one term. |

The set of subjects in effect for an arm in a term is: the active level mappings for that term, plus that arm's include exceptions, minus that arm's exclude exceptions. An include exception for a subject already mapped at level is rejected as redundant: **English Studies is already mapped to Primary 2 this term. This arm already takes it.** An exclude exception for a subject not mapped at level is rejected the same way.

### 6.6.5 Which path the creation screen defaults to

The mapping screen defaults to the level path, and the per-arm path is a secondary action behind a link reading Set an exception for one arm. English is English whether the pupil sits in Primary 1A or 1B, and nine times in ten the administrator wants the level. Presenting both paths equally invites per-arm mappings that then drift and produce result sheets with different rows for children in the same year group.

The screen is a grid: subjects down the side, active levels across the top, ticks in the cells, for one term at a time. The administrator ticks Mathematics against all six primary levels in one pass. A Copy from action fills the grid from another term or another session in one click, which is how the administrator handles Second Term in about four seconds. Saving writes and ends mappings to match the grid, in one transaction, and shows a summary of what changed before committing: **This will add 12 mappings and end 2 mappings. Primary 4 will stop taking Handwriting. Continue?**

Below the grid, a warning panel lists every arm whose subject set differs from its level, so unintended divergence is visible: **Primary 2B takes French, which Primary 2A does not. Result sheets for these two arms will show different subjects.**

### 6.6.6 Unmapping a subject after marks have been entered

Ending a mapping for the active term when `subject_score` rows exist for that subject in any arm under the level is rejected. The message names the arms and the counts: **Marks have been entered for Handwriting in Primary 4A (28 pupils) and Primary 4B (26 pupils) this term. Ending this mapping now would hide marks that have already been recorded. End it for Second Term instead, or void the marks first.**

The administrator has two clean routes. The first is to end the mapping from the following term, which is a normal save against that term's grid and touches nothing already recorded. The second is for a genuine error, where a subject was mapped and scored that the level does not take: a Super Admin voids the marks using `result.score.void` with a reason, which writes each voided value to the audit log, and the mapping can then be ended. Voiding is per subject per arm per term and is a single action on the score sheet, not a per-cell exercise.

Ending a mapping for a term that is already closed is refused outright, without an escape route: **First Term 2026/2027 is closed. Its subject mappings cannot be changed.**

Deactivating a subject does not end its mappings. It stops new mappings only. A subject deactivated in November continues to appear on Primary 3's sheet until the First Term mapping ends naturally at the term boundary, which is the behaviour that keeps the result sheet stable through the term.

### 6.6.7 List views

The subject list carries: name, code, status, number of levels mapped this term, number of arm exceptions this term, and number of pupils currently taking it. The last column is what tells an administrator that Nigerian Language is mapped to Primary 1 only and is therefore probably a mistake. Filters: status, level, term. Default sort by name ascending. The mapping grid described above is a separate screen reached from this list and from the arm detail view.

### 6.6.8 Error and edge cases

| Case | Behaviour |
| --- | --- |
| Subject mapped to a level with no arms | Allowed. The administrator maps ahead of creating arms and the mapping applies as soon as an arm exists. |
| Arm created after mappings exist for its level | Inherits them automatically. No action needed. |
| Subject deleted | Permitted only where it has never been mapped and never scored. Otherwise deactivate, per the single rule in 6.4.2. |
| Two subjects with the same code | Rejected: The code MTH is already used by Mathematics. |
| Copy from a term with 14 mappings into a term that already has 3 | The preview shows 11 additions and 0 endings, and does not duplicate the 3. |
| Exception created on an arm in a closed session | Rejected. |
| Core subject for promotion unmapped from a level | Allowed, but the promotion proposal for pupils at that level will report the core subject as not taken rather than failed, and proposes on the average alone. The promotion review screen flags those rows. |

### 6.6.9 Endpoints

| Endpoint | Notes |
| --- | --- |
| GET /subjects, POST /subjects, PATCH /subjects/{id}, DELETE /subjects/{id} | Subject CRUD with the delete rule. |
| GET /subject-mappings?term_id= | The whole grid for a term: levels, subjects, ticks, and the arm exception summary. |
| PUT /subject-mappings?term_id= | Whole-grid save. Atomic. Supports dry_run returning the additions and endings summary. |
| POST /subject-mappings/copy | Body carries source term and destination term. |
| GET /arms/{id}/subjects?term_id= | The resolved set in effect for one arm, with each row flagged as level-inherited or arm exception. This is the endpoint the score entry screen and the result renderer both call. |
| POST /arms/{id}/subject-exceptions, DELETE /subject-exceptions/{id} | Exception management. |


---
