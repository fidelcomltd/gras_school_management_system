# 4. Actors and Privilege Matrix

## 4.1 Actors

Everybody who logs in is an admin account. There is no separate teacher table, no separate bursar table and no parent table. What differentiates one person from another is the role they hold and the arms that role is scoped to. This keeps the account model to one table and puts all the variation in roles, which the school can edit.

| Actor | Authenticates? | What they do |
| --- | --- | --- |
| Super Admin | Yes | Holds every privilege school-wide. Creates other admins, builds roles, edits settings. At least one active Super Admin exists at all times and the system enforces it. |
| School Administrator | Yes | Runs the school's records: sessions, terms, levels, arms, subjects, pupils, guardians, promotion. Does not edit the grading scale or the assessment structure and does not approve results. |
| Head Teacher | Yes | Reviews computed results for an arm, writes the head teacher's remark, approves or returns, publishes. Reads every report. Does not enter marks. |
| Class Teacher | Yes | Enters continuous assessment and examination marks, trait ratings, attendance figures and the class teacher's remark, for the arms their assignment is scoped to and no others. |
| Bursar | Yes | Generates, prints, revokes and reports on access pins. Reads the pupil register. Cannot see or enter marks. |
| Auditor | Yes | Read-only across records, results and the audit log. Holds no write privilege at all. Used by the proprietor and by anybody reviewing a disputed result. |
| Parent or guardian | No | Anonymous holder of a registration number and a pin. Reads and downloads a published result on the public portal. Has no account and no identity in the system. |

## 4.2 The scope model, and how Class Teacher is expressed

This is the single most consequential decision in the admin module, so it is settled here and referenced everywhere else. Privileges do not carry scope. Role assignments do.

A privilege is a flat string, `result.score.enter`. A role is a named set of privileges, Class Teacher. An assignment ties an admin account to a role and gives that pairing a scope. Scope is one of two things: school-wide, or a list of specific arms in a specific session.

| Concept | Definition |
| --- | --- |
| Privilege | An immutable string of the form `module.action`, shipped with the code. Admins cannot create privileges. Each privilege carries a boolean `scopable` set by the code, not by the admin. |
| Role | An admin-created named set of privileges. A role has no scope of its own. |
| Assignment | A record joining one admin account, one role, one session, and either the flag school_wide or a set of arm identifiers. An admin may hold several assignments, for example Class Teacher over Primary 2A and Primary 2B. |
| Effective privilege set | Resolved per request. The union of all privileges from all active assignments held by the account, each tagged with the scope it arrived through. |

A privilege marked scopable is checked against the scope of the assignment that granted it. A privilege not marked scopable is only ever granted school-wide, and an attempt to create an arm-scoped assignment for a role containing a non-scopable privilege is rejected at save time with the message: **This role contains privileges that cannot be limited to an arm: settings.grading.update. Remove them from the role, or assign the role school-wide.**

So Class Teacher is a role holding `result.score.enter`, `result.trait.enter`, `result.attendance.enter`, `result.remark.classteacher`, `result.compute`, `result.submit`, `result.view`, `pupil.view` and `contact.view`, all of which are scopable, assigned to Mrs Adeyemi with scope Primary 2A for session 2026/2027. She opens the score entry screen and sees Primary 2A only. Primary 2B does not appear in her arm selector, and a hand-crafted request naming Primary 2B is rejected by the server with 403 and no data leakage in the body.

### 4.2.1 How the server resolves scope

Every request that touches pupil or result data resolves a target arm before the privilege check runs. The resolution rules are fixed:

- A request naming an arm resolves to that arm.
- A request naming a pupil resolves to the pupil's arm of record for the active term.
- A request naming a result set resolves to that result set's arm.
- A request naming a level, with no arm, requires the privilege school-wide. An arm-scoped holder cannot perform level-wide operations even over a level containing only their own arm.

The check is then: does the account hold the required privilege through at least one active assignment whose scope is school-wide, or whose arm list contains the resolved arm, in the session the target belongs to. Failure returns 403. This check runs in server middleware on every route, not in the client. Hiding a menu item is not a privilege check and is never described as one in this document.

### 4.2.2 Scope and session boundaries

An assignment names a session. When 2027/2028 opens, last session's Class Teacher assignments do not carry over, because form teacher allocation changes every year and quietly carrying it forward would leave a teacher able to edit an arm she no longer teaches. Bulk action `Copy assignments to new session` exists for the case where allocation genuinely has not changed, and it presents the list for review before writing. A school-wide assignment carries a session too, and the same rule applies, except for the Super Admin role, whose assignment is sessionless and permanent.

## 4.3 Separation of settings authority from operational authority

Editing the grading scale is not the same authority as entering marks and must not land in the same role by default. The settings privileges are separated as their own group, every one of them non-scopable, and none of them appear in the seeded Class Teacher, Head Teacher, Bursar or Auditor roles. Head Teacher can approve and publish results but cannot change the bands that decide what a 68 is worth. School Administrator can register pupils and open terms but cannot change the assessment structure. Only Super Admin holds settings privileges on installation, and the school can create a role that holds some of them if it wants to delegate.

The practical reason is a real failure I have watched happen: a head teacher who did not like a pupil's D moved the band boundary from 50 to 48, published, and nobody knew until the next term's results looked wrong. Splitting the authority makes that a two-person act, and the audit log makes it visible.


---

## 4.4 Privilege register

This table is the security contract for the product. Every route in the system maps to exactly one privilege in this table. A route that maps to nothing is a defect. Scopable means the privilege may be granted over a list of arms rather than school-wide.

### 4.4.1 Administration and access control

| Privilege | Permits | Scopable | Seeded to |
| --- | --- | --- | --- |
| `admin.view` | List and open admin accounts. | No | SA, ADM, AUD |
| `admin.create` | Create an admin account. | No | SA |
| `admin.update` | Edit an admin account's name, email, phone. | No | SA |
| `admin.suspend` | Move an account to suspended and back to active. | No | SA |
| `admin.deactivate` | Move an account to deactivated. Irreversible except by a Super Admin reactivating it. | No | SA |
| `admin.password.reset` | Force a password reset for another account. | No | SA |
| `admin.session.revoke` | Kill another account's active sessions. | No | SA |
| `role.view` | List roles and see their privilege sets. | No | SA, ADM, AUD |
| `role.create` | Create a role. | No | SA |
| `role.update` | Add or remove privileges from a role. | No | SA |
| `role.delete` | Delete a role that has no active assignments. | No | SA |
| `role.assign` | Assign a role to an account school-wide. | No | SA |
| `role.scope.assign` | Assign a role to an account over a named list of arms. | No | SA, ADM |
| `audit.view` | Read the audit log. | No | SA, AUD |
| `audit.export` | Export a filtered audit log to CSV. | No | SA, AUD |

### 4.4.2 Settings

| Privilege | Permits | Scopable | Seeded to |
| --- | --- | --- | --- |
| `settings.view` | Read every settings page. Read-only. | No | SA, ADM, HT, AUD |
| `settings.identity.update` | Edit school name, short name, address, phone, email, motto, logo, head teacher name and signature image. | No | SA |
| `settings.abbreviation.update` | Edit the school abbreviation used in registration numbers. Split out from identity because it has consequences identity fields do not. | No | SA |
| `settings.regnumber.update` | Edit serial width, separator and the reset rule for the registration number pattern. | No | SA |
| `settings.grading.update` | Add, edit, remove and reorder grading bands. Reset to seeded defaults. | No | SA |
| `settings.assessment.update` | Add, rename, remove and reorder continuous assessment components, and set the examination maximum. | No | SA |
| `settings.traits.update` | Edit the affective and psychomotor trait lists and the trait rating scale. | No | SA |
| `settings.resultrules.update` | Edit annual computation method and weights, position scope, level position visibility, tie-breaking rule, pass mark, promotion threshold. | No | SA |
| `settings.pin.update` | Edit default pin length, default maximum uses and the character set. | No | SA |
| `settings.reset.defaults` | Restore the grading scale, assessment structure or trait lists to seeded values. | No | SA |

### 4.4.3 Academic structure

| Privilege | Permits | Scopable | Seeded to |
| --- | --- | --- | --- |
| `session.view` | List sessions and terms. | No | all roles |
| `session.create` | Create a session and its three terms. | No | SA, ADM |
| `session.update` | Edit session and term dates, times school opened, resumption date. | No | SA, ADM |
| `term.open` | Move a term from upcoming to active. | No | SA, ADM |
| `term.close` | Move the active term to closed. | No | SA, ADM |
| `promotion.run` | Run end-of-session promotion for a level or the whole school. | No | SA, ADM |
| `promotion.reverse` | Reverse a promotion batch. | No | SA |
| `level.view` | List and open class levels. | No | all roles |
| `level.create` | Create a class level and place it in the progression chain. | No | SA, ADM |
| `level.update` | Rename a level, change its section, reorder it, change its next level. | No | SA, ADM |
| `level.deactivate` | Deactivate or reactivate a level. | No | SA, ADM |
| `level.delete` | Hard delete a level that nothing has ever referenced. | No | SA |
| `arm.view` | List and open arms. | Yes | all roles |
| `arm.create` | Create an arm under a level for a session, singly or in bulk. | No | SA, ADM |
| `arm.update` | Edit an arm's label, capacity and status. | No | SA, ADM |
| `arm.formteacher.assign` | Set or change the form teacher on an arm. | No | SA, ADM |
| `arm.delete` | Hard delete an arm that has never held an enrolment. | No | SA |
| `arm.capacity.override` | Enrol a pupil into an arm that is already at capacity. | Yes | SA, ADM |

### 4.4.4 Pupils, guardians and subjects

| Privilege | Permits | Scopable | Seeded to |
| --- | --- | --- | --- |
| `pupil.view` | List and open pupil records. | Yes | all roles |
| `pupil.create` | Register a pupil and issue a registration number. | No | SA, ADM |
| `pupil.update` | Edit pupil biographical fields. | Yes | SA, ADM |
| `pupil.photo.update` | Upload or replace a pupil photograph. | Yes | SA, ADM, CT |
| `pupil.status.update` | Change status between active, transferred, withdrawn, graduated. | No | SA, ADM |
| `pupil.transfer` | Move a pupil between arms, singly or in bulk. | No | SA, ADM |
| `pupil.import` | Run a bulk import from spreadsheet. | No | SA, ADM |
| `pupil.regnumber.correct` | Correct a wrongly issued registration number. | No | SA |
| `pupil.admission.approve` | Approve a pending admission, moving it to active and issuing the registration number, per 6.5.11. | No | SA, ADM, HT |
| `pupil.safeguarding.view` | Read the section F health block and the barred-persons list, per 6.5.6 and 6.5.7. Every read is audited. Deliberately withheld from the Bursar and the Auditor. | Yes | SA, ADM, HT, CT |
| `pupil.safeguarding.update` | Edit the health block and the barred-persons list. | Yes | SA, ADM, HT |
| `pupil.document.manage` | Tick off and attach files against the section H admission document checklist. | Yes | SA, ADM |
| `contact.create` | Add a parent, guardian or emergency contact. Replaces `guardian.create`, since one entity now serves all five contact roles per 6.5.5. | Yes | SA, ADM |
| `contact.update` | Edit a contact. Replaces `guardian.update`. | Yes | SA, ADM |
| `weekly.view` | List and read weekly report sheets, per 6.10. | Yes | all roles |
| `weekly.enter` | Write and edit weekly report day notes. | Yes | SA, ADM, HT, CT |
| `weekly.publish` | Publish or unpublish a week to the parent portal. | Yes | SA, ADM, HT, CT |
| `pupil.delete` | Soft delete a pupil record that has no result history. | No | SA |
| `contact.view` | See contact names, phone numbers and relationships across all five roles in 6.5.5. Formerly `guardian.view`; the old name is retained as an alias in the seed data so existing role assignments do not break. | Yes | SA, ADM, HT, CT, BUR |
| `subject.view` | List subjects and see mappings. | Yes | all roles |
| `subject.create` | Create a subject. | No | SA, ADM |
| `subject.update` | Edit subject name, code, description. | No | SA, ADM |
| `subject.deactivate` | Deactivate or reactivate a subject. | No | SA, ADM |
| `subject.delete` | Hard delete a subject never mapped and never scored. | No | SA |
| `subject.map` | Map a subject to a level for a session and term. | No | SA, ADM |
| `subject.map.arm` | Create a per-arm exception to a level mapping. | No | SA, ADM |
| `subject.unmap` | End a mapping. | No | SA, ADM |

### 4.4.5 Results

| Privilege | Permits | Scopable | Seeded to |
| --- | --- | --- | --- |
| `result.view` | Open a score sheet or a computed result. | Yes | SA, ADM, HT, CT, AUD |
| `result.score.enter` | Enter and edit continuous assessment and examination marks while the result set is Draft or Returned for Correction. | Yes | SA, CT |
| `result.score.void` | Void an already entered mark with a stated reason, so that a mapping can be ended or an error unwound. | Yes | SA |
| `result.trait.enter` | Enter affective and psychomotor ratings. | Yes | SA, CT |
| `result.attendance.enter` | Enter times present and times absent per pupil. | Yes | SA, CT |
| `result.remark.classteacher` | Write or edit the class teacher's remark. | Yes | SA, CT |
| `result.remark.headteacher` | Write or edit the head teacher's remark. | No | SA, HT |
| `result.compute` | Run computation over a result set. | Yes | SA, CT, HT |
| `result.submit` | Move a result set from Draft to Awaiting Approval. | Yes | SA, CT |
| `result.approve` | Move a result set from Awaiting Approval to Approved. | No | SA, HT |
| `result.return` | Return a result set to the class teacher with a reason. | No | SA, HT |
| `result.publish` | Publish an approved result set and write the configuration snapshot. | No | SA, HT |
| `result.unpublish` | Withdraw a published result set from the parent portal. | No | SA |
| `result.annual.compute` | Compute annual cumulative results for an arm once Third Term is published. | No | SA, HT |
| `result.print` | Render and download the result PDF from inside the back office. | Yes | SA, ADM, HT, CT |
| `promotion.decide` | Override the system-proposed promotion status on a Third Term result. | No | SA, HT |

### 4.4.6 Pins and reports

| Privilege | Permits | Scopable | Seeded to |
| --- | --- | --- | --- |
| `pin.view` | List pin batches and open a batch. | No | SA, ADM, HT, BUR, AUD |
| `pin.generate` | Generate a pin batch. | No | SA, BUR |
| `pin.print` | Render the print run for a batch, which reveals pin plaintext once. | No | SA, BUR |
| `pin.revoke` | Revoke a single pin or a whole batch. | No | SA, BUR |
| `pin.usage.view` | Read the usage report for a pin, including timestamps and truncated source addresses. | No | SA, ADM, BUR, AUD |
| `report.view` | Open any report in section 10. | Yes | SA, ADM, HT, AUD |
| `report.export` | Export a report to CSV or PDF. | Yes | SA, ADM, HT, AUD |

Key to the seeded column: SA Super Admin, ADM School Administrator, HT Head Teacher, CT Class Teacher, BUR Bursar, AUD Auditor.

## 4.5 Seeded roles

Six roles ship with the system. They are ordinary editable roles, not protected structures, with one exception: Super Admin cannot be edited, renamed or deleted, because a role that can grant everything must not be quietly narrowed or widened. Everything else the school may change.

| Role | Scope | Privilege set |
| --- | --- | --- |
| Super Admin | School-wide, sessionless | Every privilege in the register. Not editable. |
| School Administrator | School-wide | All of `session.*`, `level.*` except delete, `arm.*` except delete, `pupil.*` except delete and regnumber.correct, `guardian.*`, `subject.*` except delete, `promotion.run`, `role.scope.assign`, `admin.view`, `settings.view`, `pin.view`, `pin.usage.view`, `result.view`, `result.print`, `report.*`. |
| Head Teacher | School-wide | `result.view`, `result.compute`, `result.remark.headteacher`, `result.approve`, `result.return`, `result.publish`, `result.annual.compute`, `result.print`, `promotion.decide`, `pupil.view`, `contact.view`, `pupil.safeguarding.view`, `pupil.safeguarding.update`, `pupil.admission.approve`, `weekly.view`, `weekly.enter`, `weekly.publish`, `arm.view`, `subject.view`, `session.view`, `level.view`, `settings.view`, `pin.view`, `report.*`. |
| Class Teacher | Arm-scoped | `result.view`, `result.score.enter`, `result.trait.enter`, `result.attendance.enter`, `result.remark.classteacher`, `result.compute`, `result.submit`, `result.print`, `pupil.view`, `pupil.photo.update`, `contact.view`, `pupil.safeguarding.view`, `weekly.view`, `weekly.enter`, `weekly.publish`, `arm.view`, `subject.view`, `session.view`, `level.view`. |
| Bursar | School-wide | `pin.view`, `pin.generate`, `pin.print`, `pin.revoke`, `pin.usage.view`, `pupil.view`, `contact.view`, `weekly.view`, `arm.view`, `session.view`, `level.view`. No result privilege of any kind, and no safeguarding privilege: a bursar has no reason to read a child's allergy list. |
| Auditor | School-wide | `audit.view`, `audit.export`, `settings.view`, `admin.view`, `role.view`, `session.view`, `level.view`, `arm.view`, `subject.view`, `pupil.view`, `result.view`, `pin.view`, `pin.usage.view`, `report.*`. No write privilege. |

## 4.6 Privilege matrix by module action

The register above is the authoritative list. This matrix is the same information turned the other way round, for the reader who wants to know at a glance who can do what. A tick means the seeded role holds the privilege. Class Teacher ticks are always limited to the arms the assignment names.

| Module action | SA | ADM | HT | CT | BUR | AUD |
| --- | --- | --- | --- | --- | --- | --- |
| Create and edit admin accounts | Yes | No | No | No | No | No |
| Build roles and assign school-wide | Yes | No | No | No | No | No |
| Assign a role over named arms | Yes | Yes | No | No | No | No |
| Read the audit log | Yes | No | No | No | No | Yes |
| Edit school identity and logo | Yes | No | No | No | No | No |
| Edit the abbreviation | Yes | No | No | No | No | No |
| Edit the grading scale | Yes | No | No | No | No | No |
| Edit the assessment structure | Yes | No | No | No | No | No |
| Edit trait lists and rating scale | Yes | No | No | No | No | No |
| Edit result rules and tie-breaking | Yes | No | No | No | No | No |
| Create a session, open or close a term | Yes | Yes | No | No | No | No |
| Create, rename or reorder a class level | Yes | Yes | No | No | No | No |
| Delete a class level or an arm | Yes | No | No | No | No | No |
| Create arms, singly or in bulk | Yes | Yes | No | No | No | No |
| Assign a form teacher to an arm | Yes | Yes | No | No | No | No |
| Override arm capacity on enrolment | Yes | Yes | No | No | No | No |
| Register a pupil, issue a number | Yes | Yes | No | No | No | No |
| Correct a registration number | Yes | No | No | No | No | No |
| Bulk import pupils | Yes | Yes | No | No | No | No |
| Transfer a pupil between arms | Yes | Yes | No | No | No | No |
| View guardian phone and address | Yes | Yes | Yes | Yes | Yes | No |
| Create subjects and map to levels | Yes | Yes | No | No | No | No |
| Enter marks | Yes | No | No | Yes | No | No |
| Void an entered mark | Yes | No | No | No | No | No |
| Enter traits and attendance | Yes | No | No | Yes | No | No |
| Write the class teacher's remark | Yes | No | No | Yes | No | No |
| Write the head teacher's remark | Yes | No | Yes | No | No | No |
| Run computation | Yes | No | Yes | Yes | No | No |
| Submit for approval | Yes | No | No | Yes | No | No |
| Approve or return a result set | Yes | No | Yes | No | No | No |
| Publish a result set | Yes | No | Yes | No | No | No |
| Withdraw a published result set | Yes | No | No | No | No | No |
| Compute annual cumulative results | Yes | No | Yes | No | No | No |
| Override a promotion decision | Yes | No | Yes | No | No | No |
| Run promotion for a level | Yes | Yes | No | No | No | No |
| Reverse a promotion batch | Yes | No | No | No | No | No |
| Generate and print pins | Yes | No | No | No | Yes | No |
| Revoke pins | Yes | No | No | No | Yes | No |
| Read the pin usage report | Yes | Yes | No | No | Yes | Yes |
| Open and export reports | Yes | Yes | Yes | No | No | Yes |


---
