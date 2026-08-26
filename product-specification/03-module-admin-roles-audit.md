# 6. Module Specifications

Each module below gives its purpose, the privileges that gate it, its entity fields with types and validation, its list and detail views, its create and edit flows, its state transitions, its business rules, its error and edge cases, and the endpoints it implies. The endpoint lists are the routes the interface needs, not an exhaustive internal API.

## 6.1 Admin, Roles and Privileges

### 6.1.1 Purpose

Somebody has to be able to say that Mrs Adeyemi may type marks for Primary 2A, that the head teacher may approve them, that the bursar may print pins, and that none of the three may move a grading band. This module holds the accounts, the roles that compose privileges, and the assignments that give a role an arm scope.

### 6.1.2 Actors and required privileges

| Operation | Privilege |
| --- | --- |
| List and open admin accounts | `admin.view` |
| Create an account | `admin.create` |
| Edit an account's own details | `admin.update`, or the account itself for name, phone and password |
| Suspend, reactivate, deactivate | `admin.suspend`, `admin.deactivate` |
| Force a password reset | `admin.password.reset` |
| Revoke another account's sessions | `admin.session.revoke` |
| Create and edit roles | `role.create`, `role.update` |
| Assign a role school-wide | `role.assign` |
| Assign a role over named arms | `role.scope.assign` |
| Read or export the audit log | `audit.view`, `audit.export` |

### 6.1.3 Entity: admin_account

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| id | UUID | Yes | System generated. |
| staff_name | String 120 | Yes | Two words minimum. Letters, spaces, hyphens and apostrophes only. Trimmed on save. |
| email | String 160 | Yes | Valid email format. Unique across active and suspended accounts, case-insensitive. Used as the login identifier. |
| phone | String 20 | Yes | Nigerian format. Accepts 08012345678 or +2348012345678 and normalises to +234 form on save. Rejects fewer than 11 digits in the national form. |
| password_hash | String | Yes | Argon2id. Never returned by any endpoint. |
| must_change_password | Boolean | Yes | True on creation and after a forced reset. |
| status | Enum | Yes | One of active, suspended, deactivated. Defaults to active. |
| is_super_admin | Boolean | Yes | Set only by the bootstrap process or by an existing Super Admin. Defaults false. |
| last_login_at | Timestamp | No | Written on successful login. |
| failed_login_count | Integer | Yes | Defaults 0. Reset on success. |
| locked_until | Timestamp | No | Set by the lockout rule in 6.1.11. |
| created_by | UUID | No | Null for the bootstrap account only. |
| created_at, updated_at | Timestamp | Yes | System. |

### 6.1.4 Entity: role

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| id | UUID | Yes | System. |
| name | String 60 | Yes | Unique, case-insensitive. Rejects the reserved name Super Admin on create. |
| description | String 300 | No | Free text. |
| is_system | Boolean | Yes | True only for Super Admin. A system role cannot be edited, renamed, deleted or have privileges removed. |
| privileges | Array of strings | Yes | At least one. Every entry must exist in the privilege register. Unknown strings are rejected naming the offender. |
| status | Enum | Yes | active or archived. An archived role cannot be newly assigned but existing assignments continue until the session ends. |

### 6.1.5 Entity: role_assignment

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| id | UUID | Yes | System. |
| admin_account_id | UUID | Yes | Must reference an account that is not deactivated. |
| role_id | UUID | Yes | Must reference an active role. |
| session_id | UUID | No | Required unless the role is Super Admin, in which case it must be null. |
| scope_type | Enum | Yes | school_wide or arm_list. |
| arm_ids | Array of UUID | Cond | Required and non-empty when scope_type is arm_list. Every arm must belong to session_id. Rejected if the role contains any non-scopable privilege. |
| granted_by | UUID | Yes | The acting account. Cannot equal admin_account_id. |
| status | Enum | Yes | active or revoked. |

A single account may hold several assignments. Mrs Adeyemi may be Class Teacher over Primary 2A and also Class Teacher over Primary 5B when she covers a colleague on maternity leave. Privileges union across assignments and scope is tracked per assignment, so holding Class Teacher over 2A does not extend her marks privilege to 5B unless a second assignment says so.

### 6.1.6 Bootstrap: how the first admin exists

Installation runs a seed command that creates one `admin_account` with `is_super_admin` true, `must_change_password` true, and a randomly generated password printed once to the installer's console and never stored in plaintext. The email is supplied as an argument to the command. The account has no `created_by`.

1. The seed command refuses to run a second time if any admin account already exists, and exits with the message: An administrator account already exists. Bootstrap has already run.
2. On first login the account is forced through a password change before any other screen renders. The forced change cannot be skipped by navigating directly to another route: server middleware rejects every request except the password change endpoint and logout while `must_change_password` is true.
3. After the password change the system routes the account to the first-run setup checklist in section 6.2.2.

The last Super Admin cannot be deleted, deactivated, suspended, or stripped of `is_super_admin`. Any operation that would leave zero active accounts with `is_super_admin` true is rejected before it is applied, with the message: **This is the only active Super Admin. Create and activate another Super Admin before changing this account.** The check counts accounts with status active only, so suspending the second-to-last Super Admin and then attempting to suspend the last one still fails.

### 6.1.7 Preventing privilege escalation

Four rules, all enforced server-side, all producing an audit event on rejection so that an attempt is visible even though it failed.

1. No account may create, edit or revoke a `role_assignment` where `admin_account_id` equals its own id. Rejection message: You cannot change your own roles. Ask another Super Admin.
2. No account may add a privilege to a role that it does not itself currently hold. This stops an account holding `role.update` but not `settings.grading.update` from writing the latter into a role and then assigning that role to a colleague as a proxy. Rejection message names the offending privileges: You do not hold settings.grading.update and cannot add it to a role.
3. No account may grant a scope wider than its own. An arm-scoped holder of `role.scope.assign` may only assign over arms inside its own scope. School-wide assignment requires `role.assign`, which is never granted arm-scoped because it is not scopable.
4. `is_super_admin` can only be set by an account that already has it. There is no route that sets it as a side effect of anything else, and it is not part of any role's privilege list, so a role cannot smuggle it in.

Rule 2 has one consequence worth stating: a Super Admin can always widen a role, because a Super Admin holds everything. That is accepted. The control on a Super Admin is the audit log and the fact that there are two of them, not a technical restriction.

### 6.1.8 List view: admin accounts

| Column | Why it is here | Sortable |
| --- | --- | --- |
| Staff name | The thing the reader is looking for. | Yes |
| Email | The login identifier, and how accounts are told apart when two staff share a surname. | Yes |
| Roles held | Comma-separated role names. Without this the list is unusable for the question actually being asked, which is who can do what. | No |
| Scope summary | School-wide, or a count and the first two arm names, for example Primary 2A, Primary 5B. Truncated with a tooltip on the full list. | No |
| Status | Rendered as a coloured chip. Active, Suspended, Deactivated. | Yes |
| Last login | DD/MM/YYYY HH:MM. Blank if never. This is how the school finds accounts nobody uses. | Yes |

Filters: status, role, scope arm, session, and a free-text search across staff name and email. Default sort is status ascending with active first, then staff name ascending. Deactivated accounts are excluded by default and appear when the status filter is set explicitly, because a list of thirty leavers above the six people who work here is not useful. Page size 25.

Detail view outside the list: the account's own details, its assignments each with role, session and scope, the resolved effective privilege set rendered as a read-only grouped list so an administrator can answer can she do this without reasoning about role composition, the last ten audit events by this account, and actions for reset password, revoke sessions, suspend and deactivate.

### 6.1.9 Create and edit flows

Creating an account is two steps, deliberately. Step one takes staff name, email and phone and creates the account. Step two assigns at least one role, and if the chosen role is arm-scopable the screen requires an arm selection before the button enables. An account with zero assignments can exist, and it can log in, and it will see a page saying no access has been granted to this account yet. That is preferable to blocking creation, because in practice the administrator creates six accounts on Monday and assigns arms on Wednesday once the form teacher list is settled.

The new account receives its password out of band. The system generates a temporary password, displays it once on screen with a copy button, and never displays it again. There is no email delivery in this product, because the school has no mail infrastructure it trusts and section 3.2 excludes messaging. The administrator writes it down and hands it over. This is a real weakness and it is logged in Appendix A entry 4.

Editing an account can change staff name, email and phone. Changing the email changes the login identifier, and the change is rejected if the new email collides with an active or suspended account. Roles are edited through the assignment list, not through the account form, so that every assignment change is a discrete audit event with its own reason field.

### 6.1.10 Account states

| State | Can log in | Meaning and transitions |
| --- | --- | --- |
| active | Yes | Normal. Moves to suspended by `admin.suspend`, to deactivated by `admin.deactivate`. |
| suspended | No | Temporary. Existing sessions are revoked immediately on suspension. Assignments are untouched and resume on reactivation. Used for a teacher on suspension pending a disciplinary matter. Moves to active by `admin.suspend`. |
| deactivated | No | The person has left. All active assignments are revoked as part of the transition. Sessions revoked. The row is retained because it is referenced by audit events and by `granted_by` on other assignments. Moves to active by `admin.deactivate` held by a Super Admin, which does not restore the revoked assignments: they are reassigned deliberately. |

### 6.1.11 Passwords, lockout and session handling

- Minimum twelve characters. At least one letter and one digit. No maximum below 128. No forced composition beyond that and no forced rotation, because forced rotation in a school of eight staff produces passwords ending in the month.
- The last five password hashes are retained per account and reuse is rejected.
- Five failed attempts in fifteen minutes locks the account for fifteen minutes. The message given is the same for a wrong password and an unknown email: Login details are not correct. This avoids confirming which emails exist.
- A forced reset by `admin.password.reset` sets a new temporary password, displays it once, sets `must_change_password`, and revokes every active session for the account.
- A self-service reset by the account holder is not available, because there is no email channel. The account holder telephones the administrator. This is stated as a limitation in Appendix A entry 4.
- Sessions are server-side records with a rotating opaque token in an HttpOnly, Secure, SameSite=Lax cookie. Idle timeout thirty minutes. Absolute timeout eight hours, which covers a school day. Changing a password or an assignment revokes every session for that account except the one performing the change.
- Concurrent sessions are allowed, maximum three per account, oldest evicted. A teacher moving from the staff room desktop to a phone should not be logged out of the desktop mid-entry.

### 6.1.12 Audit log

Every write against a governed entity produces one `audit_event` row inside the same database transaction as the write. If the transaction rolls back the event disappears with it, so a gap in the log is a failure, not a silent omission.

| Field | Type | Req | Content |
| --- | --- | --- | --- |
| id | BIGSERIAL | Yes | Monotonic, so ordering is unambiguous even within the same millisecond. |
| occurred_at | Timestamp with zone | Yes | Stored UTC, displayed WAT. |
| actor_admin_id | UUID | No | Null for system actions such as the nightly archive and for portal lookups. |
| actor_label | String 160 | Yes | Staff name and email captured at the time, so the entry stays readable after the account is renamed. |
| action | String 80 | Yes | The privilege string of the operation, for example `settings.grading.update`, or `portal.lookup` and `system.archive` for non-privileged actors. |
| entity_type | String 60 | Yes | For example grading_band, result_set, pupil. |
| entity_id | String 60 | No | Null for bulk actions, which instead carry a batch id in metadata. |
| outcome | Enum | Yes | success or rejected. Rejected entries are written for privilege failures and for escalation attempts. |
| before_json | JSONB | No | The prior state of changed fields only. Null on create. |
| after_json | JSONB | No | The new state of changed fields only. Null on delete. |
| reason | String 500 | No | Required for the actions listed below. |
| source_ip | String 45 | No | Truncated to /24 for IPv4 and /48 for IPv6 before storage. |
| user_agent | String 300 | No | Truncated. |

Recorded actions, at minimum: every settings change without exception, admin account creation and every state change, role creation and every privilege addition or removal, every assignment grant and revoke, level create, rename, reorder, deactivate and delete, arm create and capacity override, pupil create, registration number correction, status change and arm transfer, subject mapping and unmapping, every score entry and edit with before and after values, score voids, computation runs, submission, approval, return, publication, unpublication, annual computation, promotion runs and reversals, pin batch generation, printing and revocation, every portal lookup whether successful or failed, and every rejected privilege check.

A `reason` is mandatory on: score void, registration number correction, result return, result unpublication, promotion reversal, pin batch revocation, admin deactivation, and any settings edit the system has warned would affect published results. The save is rejected with **A reason is required for this change** if the field is blank or shorter than ten characters.

Reading the log requires `audit.view`. It is filterable by date range, actor, action, entity type and outcome, and sorted newest first by default. Entries cannot be edited or deleted through any interface, and no endpoint exists that would allow it. Retention is seven years, after which entries older than that are moved to the cold archive described in section 9.8 rather than dropped. Export to CSV requires `audit.export` and is itself an audit event.

Score entry deserves a note, because it is the log the school will actually use. Every mark change writes an event with the pupil, the subject, the component, the old value and the new value. When a parent comes to the office in March insisting their child scored 62 in Mathematics and the sheet says 26, the office can answer the question in one screen.

### 6.1.13 Error and edge cases

| Case | Behaviour |
| --- | --- |
| Email already used by a deactivated account | Allowed. Uniqueness is enforced against active and suspended accounts only, because a school reusing a departed teacher's school email address is common. |
| Assigning an arm-scoped role over an arm in a closed session | Rejected: The session 2025/2026 is closed. Assignments can only be made in an open session. |
| Assigning a role that has just been archived | Rejected: This role is archived and cannot be newly assigned. |
| An account holds Class Teacher over an arm that is later deleted | Arm deletion is only possible when no enrolment ever existed. The assignment's arm list has the row removed. If that empties the list, the assignment is revoked and an audit event records it. |
| Two Super Admins suspend each other in the same minute | The second operation fails the last-active-Super-Admin check and is rejected. The check runs inside the transaction with a row lock on the account table, not as a pre-flight read. |
| A form teacher is reassigned mid-term | The old assignment is revoked and a new one created. Marks already entered are unaffected: they belong to the result set, not to the person who typed them. The audit log retains who typed each mark. |
| An account with an active session is deactivated | Sessions are revoked within the request. The next request from that browser returns 401 and the interface routes to login with the message Your access has been withdrawn. Contact the school administrator. |

### 6.1.14 Endpoints

| Endpoint | Notes |
| --- | --- |
| POST /auth/login | Email and password. Returns session cookie. Rate limited. |
| POST /auth/logout | Revokes the current session. |
| POST /auth/password | Self-service change. Requires the current password. |
| GET /me | Account, assignments, resolved effective privilege set with scopes. |
| GET /admins | Paged, filtered list. |
| POST /admins | Create. Returns the temporary password once in the response body and never again. |
| GET /admins/{id} | Detail including assignments and effective privileges. |
| PATCH /admins/{id} | Name, email, phone. |
| POST /admins/{id}/status | Body carries target status and, for deactivation, a reason. |
| POST /admins/{id}/password-reset | Forced reset. |
| DELETE /admins/{id}/sessions | Revoke all sessions for the account. |
| GET /privileges | The privilege register, grouped by module, with the scopable flag. Read-only, no privilege required beyond authentication. |
| GET /roles, POST /roles, GET /roles/{id}, PATCH /roles/{id}, DELETE /roles/{id} | Role CRUD. PATCH on a system role returns 409. |
| GET /admins/{id}/assignments, POST /admins/{id}/assignments, DELETE /assignments/{id} | Assignment grant and revoke. |
| POST /assignments/copy-to-session | Bulk copy of assignments into a new session, with a dry-run flag that returns what would be created. |
| GET /audit | Filtered, paged, newest first. |
| GET /audit/export | CSV stream. |


---
