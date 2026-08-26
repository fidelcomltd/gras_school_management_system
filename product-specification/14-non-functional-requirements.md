# 9. Non-Functional Requirements

## 9.1 Authentication and password policy

Back-office authentication is email and password, specified in 6.1.11. The rules that belong here rather than in the module are the ones that apply system-wide.

- All traffic over HTTPS with HSTS. No endpoint answers on plain HTTP beyond a redirect.
- Passwords hashed with Argon2id, memory cost tuned so that a single hash takes between 150 and 300 milliseconds on the production instance. Cost parameters stored with the hash so they can be raised later without invalidating existing passwords.
- Session tokens are 32 bytes from a cryptographically secure source, stored hashed server-side, and rotated on privilege change and on password change.
- No token, password or pin appears in a URL, a log line, an error page or an analytics payload. Request logging redacts the password and pin fields by name before writing.
- There is no second factor in this version. The school has eight staff accounts and no mobile number verification path, and a second factor nobody can reset is a lockout waiting to happen. This is recorded as an accepted limitation in Appendix A entry 5.
- The parent portal has no authentication in the account sense. It has pin validation, specified in 6.8 and 6.9.

## 9.2 Authorisation enforcement

Every privilege check runs on the server, in middleware, before the handler executes. Hiding a menu item is presentation, not enforcement, and no requirement in this document is satisfied by hiding a menu item.

- Each route declares the privilege it requires and, where the privilege is scopable, the parameter that resolves the target arm. A route with no declared privilege fails to register at boot, so a forgotten check is a startup failure rather than a hole in production.
- Scope resolution follows 4.2.1 exactly. Where a request names a pupil, the arm is resolved from the pupil's open enrolment, not from a client-supplied arm id, because a client-supplied arm id is an attack surface.
- Failed checks return 403 with a body containing nothing but a generic message. They do not reveal whether the entity exists.
- Every 403 writes an audit event with outcome rejected, so an account probing routes it does not hold is visible.
- The interface additionally hides what the account cannot use, because showing a teacher eleven arms she cannot open is a bad interface. This is in addition to enforcement and never instead of it.
- Object-level checks run on every read as well as every write. A Class Teacher requesting a pupil id from another arm gets 403, not a filtered empty result, and not the pupil.

## 9.3 Audit logging

The audit entity, its fields, the actions recorded and the actions requiring a reason are specified in 6.1.12. Three system-wide rules apply.

- The audit write shares the transaction with the change it records. No queue, no fire-and-forget, no separate service.
- The log is append-only at the database level: the application's database role holds INSERT and SELECT on `audit_event` and no UPDATE or DELETE. Retention pruning runs under a separate administrative role on a schedule.
- Portal lookups write audit events with a null actor and the action `portal.lookup`, so the log carries the whole picture of who read what, including the anonymous reads.

## 9.4 Soft delete against hard delete

The rule from 6.4.2 governs every entity in the product: **delete is permitted only where nothing has ever referenced the row. Everything else deactivates.** This table applies it entity by entity so there is no interpretation left to the engineer.

| Entity | Hard delete allowed | Otherwise |
| --- | --- | --- |
| admin_account | Never | Deactivate. Referenced by audit events and by granted_by on assignments. |
| role | When no assignment has ever used it | Archive. |
| role_assignment | Never | Revoke. The record of who could do what and when is part of the audit trail. |
| audit_event | Never | Nothing. Pruned only by the retention schedule. |
| class_level | When no arm, no enrolment, no mapping and no result has ever referenced it | Deactivate. |
| arm | When no enrolment has ever existed | Deactivate, or closed at session end. |
| pupil | When no result and no enrolment history exists | Soft delete with a flag, excluded everywhere. The registration number is not returned to the counter. |
| guardian | When the pupil has a second guardian | A pupil must retain at least one guardian, so the last guardian cannot be deleted, only edited. |
| subject | When never mapped and never scored | Deactivate. |
| subject_mapping | When no score exists against it in that term | End it, which keeps history. |
| subject_score | Never | Void, with a reason, retaining the value. |
| result_set and computed rows | Computed rows are deleted and rewritten by every computation | The result set itself is never deleted. Withdrawn is its off state. |
| pin | Never | Revoke. |
| pin_batch | Never | Revoke. |
| academic_session, term | Never once any arm or result exists | Closed. A session created in error with nothing under it may be deleted. |
| promotion_batch | Never | Reversed, per 6.3.7. |

Soft-deleted and deactivated rows are excluded from every default list, every search, every count and every export unless the caller asks for them explicitly through a status filter. This is enforced at the data access layer with a default scope, not remembered per query.

## 9.5 Pagination, search and list conventions

- Cursor pagination on every list endpoint, with the cursor opaque to the client. Offset pagination is not used, because a register that grows while an administrator pages through it will skip rows.
- Default page size 25, except the pupil list at 50 and the score sheet, which is never paged: an arm is at most a hundred pupils and a teacher entering marks must not page.
- Every list endpoint accepts sort and direction, validated against a whitelist of sortable fields per endpoint.
- Search is case-insensitive and accent-insensitive, matches on substring rather than prefix, and trims input. Nigerian names are frequently typed without diacritics and a prefix-only match on surname makes the box useless.
- Search responses indicate which field matched, so a hit on a guardian phone number is not confusing.
- Every list is scope-filtered before pagination, so a Class Teacher's page one is her pupils and not an empty page of somebody else's.
- Filters persist across navigation within a session, because an administrator who filters to Primary 3A, opens a pupil and comes back expects to still be in Primary 3A.

## 9.6 File uploads

| Upload | Constraints |
| --- | --- |
| Pupil photograph | JPEG or PNG. Maximum 3 MB accepted from the client. Client-side downscaling to 800 pixels on the long edge before upload, so a phone camera image does not consume a parent's or a clerk's data allowance. Server re-encodes to a 400 by 400 centre-cropped JPEG at quality 80 and a 96 pixel thumbnail, discards the original, and strips all EXIF including GPS. Stripping GPS is not optional: a photograph taken at a child's home carries the home's coordinates. |
| School logo | PNG or JPEG. Maximum 2 MB. Minimum 300 by 300. Derivatives at 200 and 64 pixels. Transparency preserved for PNG. |
| Head teacher signature | PNG preferred for transparency, JPEG accepted. Maximum 1 MB. Recommended 600 by 200. Rendered at a fixed height in the PDF, so an image of the wrong aspect ratio is letterboxed rather than distorted. |
| Bulk import spreadsheet | XLSX or CSV. Maximum 5 MB. Maximum 5000 rows. Parsed server-side. |
| All uploads | Content type verified by inspecting the file's magic bytes, not by trusting the declared type or the extension. SVG is rejected outright for every image field, because an SVG is a script container. Files are stored outside the web root under generated names, served through an endpoint that applies the same privilege check as the parent record, and served with Content-Disposition and a nosniff header. |

## 9.7 Timezone, dates and numbers

- All timestamps stored in UTC. All timestamps displayed in West Africa Time, UTC+1. There is no daylight saving in Nigeria, so no ambiguity arises.
- Dates display as DD/MM/YYYY everywhere: interface, PDF, CSV export, pin slip, audit log. Never MM/DD/YYYY, and never a written month name in a data field, because a school pasting a CSV into Excel needs a parseable date.
- Times display as 24 hour, HH:MM.
- Date inputs accept DD/MM/YYYY typed and offer a calendar picker. A two-digit year is rejected rather than interpreted.
- Marks are integers. Averages carry two decimal places and class averages one, with a trailing zero shown, so 80.00 prints as 80.00 and not as 80.
- The term end date is the reference point for a pupil's printed age, per 6.5.3.

## 9.8 Performance, connectivity and backup

### 9.8.1 Performance targets

| Operation | Target |
| --- | --- |
| Back-office page load, warm cache, 3G | Under 3 seconds to interactive. |
| Score sheet load for a 30-pupil arm | Under 2 seconds. |
| Score sheet save, whole sheet | Under 1.5 seconds at the 95th percentile. |
| Computation for one arm, 30 pupils, 9 subjects | Under 4 seconds. |
| Computation for a whole level with a level position, 3 arms | Under 10 seconds. |
| Annual computation for one arm | Under 5 seconds. |
| Parent portal validation | Under 1 second at the 95th percentile, plus the deliberate 400 millisecond pad. |
| Result PDF, first generation | Under 3 seconds. From cache under 500 milliseconds. |
| Bulk import validation, 500 rows | Under 20 seconds, run as a background job with progress. |

### 9.8.2 Working on a weak connection

- The score entry sheet holds unsaved marks in browser memory and retries a failed save with backoff, showing a persistent banner while unsaved work exists. It warns on navigation away with unsaved changes. A teacher who loses forty minutes of typing once will not use the system again, and this is the single most important resilience requirement in the product.
- No screen requires more than one round trip to become usable. The score sheet ships its pupils, its components and its existing marks in one response.
- Payloads are gzipped or brotli compressed. Images are served at the size they are displayed, never full size with CSS scaling.
- Idempotency keys on every mutating endpoint that a retry could duplicate, in particular pupil registration, pin generation, promotion commit and result publication. A parent-side retry must not issue two registration numbers or two pin batches.

### 9.8.3 Backup, archive and restore

- Automated nightly full database backup, retained 30 days. Weekly backup retained 12 weeks. Monthly backup retained 24 months.
- Point-in-time recovery through continuous write-ahead log archiving, with a recovery point objective of 15 minutes and a recovery time objective of 4 hours.
- Uploaded files backed up on the same schedule as the database, and restored consistently with it: a restore that brings back pupil rows without their photographs is a failed restore.
- A restore is rehearsed once per session onto a scratch instance, and the rehearsal is signed off by whoever administers the system. An untested backup is not a backup.
- End-of-session archive: when a session closes, a job produces a single archive bundle for the session containing every published result as a PDF, a CSV of the broadsheet per arm per term, a CSV of the pupil register with enrolments, the promotion decisions, and a copy of the configuration snapshots. The bundle is downloadable by a Super Admin and is the artefact the school keeps off-site. It exists because a school that loses its database should not lose ten years of results, and because the proprietor will one day want the 2026/2027 results without needing the application to still exist.
- The archive does not delete anything. Closed sessions stay fully queryable in the live system, and published results stay on the parent portal indefinitely.

## 9.9 Nigeria Data Protection Act 2023

The system holds personal data about children and about their parents and guardians. The children are minors and cannot consent for themselves, and a public unauthenticated portal exposes their academic records to anybody holding a printed slip. The obligations below are requirements on the build, not advice.

| Obligation | How it is met |
| --- | --- |
| Lawful basis | Processing of pupil records rests on the performance of the contract between the school and the parent, together with the school's legitimate interest in maintaining an academic record. Health data, meaning blood group, genotype and the medical note, rests on explicit parental consent captured at admission, and is optional in the system precisely so that a school without that consent can leave it blank. |
| Data minimisation | The portal payload carries only what the result sheet prints. The photograph is excluded from the portal PDF by default. The medical note, the guardian's address, the guardian's occupation and the pupil's health fields never reach the portal or the verification page under any setting. |
| Purpose limitation | No pupil or guardian data is used for messaging, marketing or any purpose outside producing and issuing results. Section 3.2 excludes messaging entirely, which removes the temptation. |
| Retention | Academic records, meaning results, enrolments and registration numbers, are retained for 10 years after a pupil leaves, which covers the period in which a former pupil may need a testimonial. Guardian contact details are purged 24 months after a pupil's status moves to transferred, withdrawn or graduated, since the school has no lawful need for a phone number after that. Health fields are purged at the same point. Portal attempt logs are purged after 90 days. Pin ciphertext is purged 30 days after generation. Audit events are retained 7 years. A scheduled job performs each purge and writes an audit event recording what it removed. |
| Access control and logging | Every read of a pupil record by an authenticated account is subject to the scope rules in 4.2, and every read of a result through the portal writes an audit event. The school can therefore answer who looked at this child's record. |
| Data subject rights | A parent may request access to, correction of, or erasure of their own contact details and their child's record. Access is satisfied by exporting the pupil's full record and result history from the pupil detail view. Correction is an ordinary edit. Erasure is satisfied by purging guardian contact details and health fields, while academic records are retained under the school's legal and legitimate-interest basis and the parent is told so in the school's notice. There is no self-service portal for these requests: they arrive on paper at the office, and the system's job is to make them easy to fulfil. |
| Children's data specifically | No pupil ever holds an account. No pupil photograph is published. The portal never reveals a pupil's name until a pin has validated, so a wrong guess discloses nothing. The verification page shows initials rather than a full name. Because pins are unbound, per 6.8.2, the school carries a heightened duty here: the maximum-uses setting is the control that limits how many children one slip can expose, and it should be set at 3 and reviewed each session. |
| The public portal risk, stated plainly | An unauthenticated portal that returns a child's academic record on presentation of a printed slip is a residual risk that cannot be removed without giving parents accounts, which section 3.2 excludes. The school has further directed that pins be unbound, per 6.8.2, so a slip is not a key to one child's record but a key to any child's record, up to the number of uses on it, chosen by whoever holds the paper. Registration numbers are sequential and publicly visible, so no guessing is needed to make use of one. The mitigations are: a low maximum-uses value per batch, which is the primary control and defaults to 3; automatic suspension of a pin that opens more than three distinct pupils; per-number and per-address rate limiting; uniform failure messaging; no enumeration surface; search engine exclusion; batch revocation; and a usage report that names every pupil each pin opened, so the school can tell a parent exactly what was disclosed. The school must accept this risk in writing before launch, and the wording it signs must describe the unbound design rather than the bound one. Appendix B question 7 records it. |
| Breach handling | A suspected breach is reportable to the Nigeria Data Protection Commission within 72 hours. The system supports the school's obligation by retaining the audit log and the portal attempt log, which together establish scope and timing. A documented procedure naming who assesses and who reports is the school's to write, and Appendix B question 8 asks for the named person. |
| Data location and processors | Hosting region and any third-party processor must be recorded in the school's processing register. The build introduces no analytics, no advertising and no third-party script on any page, so the processor list is the hosting provider alone. |

One design consequence deserves naming. The decision to exclude the pupil's photograph from the portal PDF by default was taken on minimisation grounds, and the school may switch it on. If it does, a printed result sheet circulating outside the school carries a child's photograph beside their name, class and home area. The setting therefore carries the warning: **Including the photograph means every printed result sheet shows the child's face. Most schools leave this off.**

## 9.10 Printing

Result sheets and pin slips are physical artefacts in this school. They are printed on a shared inkjet or taken to a business centre, and they are photocopied.

- Every printable artefact is A4 portrait. Nothing in the product prints on Letter, and no layout assumes a printer margin narrower than 10 mm.
- Result sheet: one page per term where the subject count allows, per 6.9.6. Monochrome readable. No colour is load-bearing anywhere.
- Pin slips: four to an A4 page with cut lines, per 6.8.9.
- Broadsheet: A4 turned to the wide orientation is permitted for this one artefact, because a broadsheet of nine subjects across thirty pupils does not fit portrait. It repeats the header row on every page and prints the arm display name, the term and the session in the running header.
- Admission slip: half A4, printed one to a page with the lower half blank, since the school files them.
- Every printed artefact carries the date and time it was printed and, except for the parent's result sheet, the name of the account that printed it. A broadsheet found on a desk should be traceable.
- Print styles are proper print CSS or server-rendered PDF. No screen layout is sent to a printer and hoped for.
- Batch printing: the head teacher can print every result sheet for an arm as one PDF, and every sheet for a whole level as one PDF, so an end-of-term print run is two actions rather than thirty.


---
