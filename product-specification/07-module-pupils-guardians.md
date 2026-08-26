# Module 6.5: Pupil Management

**Design reference:** `assets/06-pupil-admission-form-page1.png`, `assets/07-pupil-admission-form-page2.png`, `assets/08-pupil-admission-form-page3.png` (Pupil Admission & Information Form, sections A to J). The form is the school's own and it defines what a complete pupil record contains.

## 6.5 Pupil Management

### 6.5.1 Purpose

The pupil register is the record the school will still be answering questions from in ten years, when a former pupil needs a testimonial and a result. It holds the child, the people responsible for the child, the registration number and the dated history of which arm the child sat in.

It also holds the information the school needs in order to keep a child safe on an ordinary Tuesday: who may collect them, who may not, what they are allergic to, and which hospital to drive to. That half of the record has no bearing on any result and is the half that matters most on the day it is needed.

### 6.5.2 What a complete pupil record contains

The admission form has ten lettered sections. Each maps to a defined place in the data model. An orchestrator planning tasks should treat this table as the contents page for the whole module.

| Form section | Filled by | Maps to | Required for admission approval? |
|---|---|---|---|
| A. School Use Only | School office | `admission_record` plus fields on `pupil` | Yes |
| B. Pupil's Information | Parent or guardian | `pupil` | Yes |
| C. Parent / Guardian Information | Parent or guardian | `pupil_contact` rows with role father, mother, guardian | Yes, at least one |
| D. Emergency Contact | Parent or guardian | `pupil_contact` rows with role emergency_primary, emergency_alternate | Yes, primary only |
| E. Authorised Persons to Pick Up | Parent or guardian | `authorised_pickup_person`, plus `barred_person` for the exclusion question | No, but the exclusion question must be answered |
| F. Health & Safety | Parent or guardian | `pupil_health` | Yes, as an explicit answer, including an explicit No |
| G. Other Important Information | Parent or guardian | `pupil.other_information` | No |
| H. Document Checklist | School office | `pupil_document` | No, tracked as outstanding |
| I. Parent / Guardian Declaration | Parent or guardian | `admission_record` declaration fields | Yes |
| J. School Confirmation | School office | `admission_record` approval fields | Yes, and it is the approval itself |

The important consequence: **a pupil record is created in a `pending` state and becomes `active` only when section J is completed.** The school can therefore start a record with a surname and finish it a week later without a half-built pupil appearing on a class register in the meantime. This replaces the single-screen registration flow the earlier version of this module specified.

### 6.5.3 Actors and required privileges

| Operation | Privilege |
|---|---|
| List and open pupils | `pupil.view`, arm-scoped for a Class Teacher |
| Start an admission | `pupil.create` |
| Edit biographical fields | `pupil.update`, arm-scoped |
| Upload a photograph | `pupil.photo.update`, arm-scoped |
| Approve an admission, moving pending to active | `pupil.admission.approve` |
| Change status | `pupil.status.update` |
| Transfer between arms | `pupil.transfer` |
| Bulk import | `pupil.import` |
| Correct a registration number | `pupil.regnumber.correct`, Super Admin only |
| Add and edit contacts | `contact.create`, `contact.update` |
| View and edit health and safeguarding data | `pupil.safeguarding.view`, `pupil.safeguarding.update` |
| Upload and mark off admission documents | `pupil.document.manage` |

`pupil.safeguarding.view` is new and deliberately narrow. It gates the health fields in section F and the barred-persons list in section E. It is seeded to Super Admin, School Administrator, Head Teacher and Class Teacher over their own arms. It is **not** granted to the Bursar or the Auditor: a bursar has no reason to read a child's allergy list, and an auditor's read-everything remit stops at health data about minors. Every read of these fields writes an audit event, unlike ordinary pupil reads.

### 6.5.4 Entity: pupil

| Field | Type | Req | Validation |
|---|---|---|---|
| id | UUID | Yes | System. |
| registration_number | String 24 | No until approval | Issued per 6.5.10 at admission approval, not at record creation. Unique. Immutable after issue. Null while pending. |
| surname | String 60 | Yes | Letters, spaces, hyphens, apostrophes. Trimmed. Stored as typed, displayed uppercase on the result sheet. |
| first_name | String 60 | Yes | Same character rule. |
| middle_name | String 60 | No | Same character rule. |
| sex | Enum | Yes | Male or Female. |
| date_of_birth | Date | Yes | In the past. Must give an age between 2 and 20. Rejection: A date of birth of 03/05/2024 makes this pupil 2 years old. Check the date. |
| age | Derived | n/a | Not stored. Whole years, computed as at the term end date for any result sheet. |
| nationality | String 60 | Yes | Free text with an autocomplete. Defaults Nigerian. |
| state_of_origin | String 60 | Yes | Picked from the 36 states plus the Federal Capital Territory. Free text is not accepted, because this field is reported on and typed spellings make that impossible. |
| lga | String 80 | Yes | Local Government Area. The picker is filtered by the selected state, which is the only way this field gets entered correctly. |
| home_address | String 300 | Yes | Multi-line permitted. This is the child's home address, distinct from any contact's own address. |
| previous_school | String 160 | No | Required where `admission_type` is returning or where the pupil is admitted above the entry level. Blank is correct for a child entering Nursery 1. |
| previous_class | String 60 | No | Same rule as previous_school. |
| photograph | Image | No | JPEG or PNG, maximum 3 MB on upload, downscaled client-side per 9.6. Server stores a 400 by 400 square and a 96 pixel thumbnail, discards the original, strips EXIF. Not required, because the school photographs a new intake in batches weeks after admission. |
| status | Enum | Yes | pending, active, transferred, withdrawn, graduated. Defaults pending. See 6.5.14. |
| other_information | Text 1000 | No | Section G of the form. Free text, whatever the parent thought the school should know. Surfaced on the detail view with a marker when present. |
| created_at, updated_at, created_by | Timestamp, Timestamp, UUID | Yes | System. |

`blood_group`, `genotype` and `medical_note` have moved off this entity to `pupil_health` in 6.5.7, where they sit with the rest of the section F answers under one privilege.

### 6.5.5 Entity: pupil_contact

The form names five contact slots: Father, Mother, Guardian if applicable, Primary Emergency Contact and Alternative Emergency Contact. The earlier version of this module allowed at most two guardians, which cannot hold this. One entity with a role now serves all five.

| Field | Type | Req | Validation |
|---|---|---|---|
| id | UUID | Yes | System. |
| pupil_id | UUID | Yes | Parent record. |
| role | Enum | Yes | father, mother, guardian, emergency_primary, emergency_alternate. At most one row per role per pupil. |
| full_name | String 120 | Yes | Two words minimum. |
| relationship | String 60 | Conditional | Required for roles guardian, emergency_primary and emergency_alternate, where the form asks for it explicitly. Implied and hidden for father and mother. |
| phone | String 20 | Yes | Nigerian format, normalised on save to a canonical form while displaying as typed. Required on every contact without exception, because it is the only channel the school has. |
| whatsapp_number | String 20 | No | Captured for father and mother, where the form asks for it. Offered with a `same as phone` tick, because it usually is and retyping it is how it gets mistyped. |
| occupation | String 80 | No | Captured for father and mother only. |
| email | String 160 | No | Valid format if supplied. Genuinely optional: most parents at this school do not have one. |
| is_primary_contact | Boolean | Yes | Exactly one contact per pupil has this true, and it must be a father, mother or guardian row rather than an emergency row. This is the person the school telephones first and the person the office hands a pin slip to. |
| created_at, updated_at | Timestamp | Yes | System. |

Rules:

- **At least one of father, mother or guardian is required.** A pupil with no responsible adult recorded cannot be approved.
- **The primary emergency contact is required.** It may be the same person as the father or mother, in which case a `copy from father` action fills it rather than the office retyping it. It is stored as its own row even when duplicated, because the form treats the slots separately and the school reads them separately in an emergency.
- The alternative emergency contact is optional but prompted, since the point of an alternative is that the primary is unreachable.
- The last remaining father, mother or guardian row cannot be deleted, only edited, per 9.4.
- A contact phone number shared across several pupils is expected and correct. Four siblings share a father's number, and the search in 6.5.15 uses that to find families.

### 6.5.6 Entities: authorised_pickup_person and barred_person

Section E. Two separate entities, because they answer opposite questions and the second is far more sensitive than the first.

**`authorised_pickup_person`**, a repeating list, zero or more rows per pupil:

| Field | Type | Req | Validation |
|---|---|---|---|
| id, pupil_id | UUID | Yes | System. |
| full_name | String 120 | Yes | Two words minimum. |
| relationship | String 60 | Yes | Free text. |
| phone | String 20 | Yes | Nigerian format. |
| display_order | Integer | Yes | The order the parent listed them, preserved. |

The interface offers three blank rows by default, matching the form's table, with an add-row action. A parent listing nobody is not blocked: the school's fallback is that only a recorded contact may collect the child, and the gate screen says so.

**`barred_person`** answers `Is there anyone who should NOT be allowed to collect the child?`:

| Field | Type | Req | Validation |
|---|---|---|---|
| id, pupil_id | UUID | Yes | System. |
| has_barred_persons | Boolean | Yes | The explicit yes or no. Stored even when No, so answered-no is distinguishable from never-asked. |
| full_name | String 120 | Conditional | Required where has_barred_persons is true. |
| details | Text 500 | No | The form's `name / relevant information` line. |

This is the most sensitive field in the system. It may record a family court order or an estrangement, and disclosing it to the wrong person could put a child at risk. It is gated behind `pupil.safeguarding.view`, excluded from every export except the safeguarding report, never printed on any sheet, never sent to the portal, and every read is audited by actor and timestamp.

### 6.5.7 Entity: pupil_health

Section F, one row per pupil, all under `pupil.safeguarding.view`.

| Field | Type | Req | Validation |
|---|---|---|---|
| id, pupil_id | UUID | Yes | System. |
| has_allergy | Boolean | Yes | Explicit yes or no. No default: the form asks, and an unanswered question is not the same as a No. |
| allergy_details | Text 500 | Conditional | Required where has_allergy is true. |
| has_medical_condition | Boolean | Yes | Explicit yes or no. |
| medical_condition_details | Text 1000 | Conditional | Required where true. |
| takes_regular_medication | Boolean | Yes | Explicit yes or no. |
| medication_details | Text 500 | Conditional | Required where true. |
| special_instructions | Text 1000 | No | The form's `special health, dietary or safety instruction` line. Covers halal and vegetarian diets, inhaler handling, and anything else. |
| preferred_hospital | String 160 | No | Strongly prompted. A blank here is a blank on the day it is needed. |
| hospital_phone | String 20 | No | Nigerian format. |
| blood_group | Enum | No | A+, A-, B+, B-, AB+, AB-, O+, O-. Free text is not accepted. |
| genotype | Enum | No | AA, AS, SS, AC, SC. |

The three yes-or-no questions are required as explicit answers and the detail fields are conditional on them. This pattern is worth preserving exactly: a school seeing `has_allergy = false` knows the question was asked, where a school looking at an empty allergy field knows nothing.

**Where this data may and may not go.** Visible to accounts holding `pupil.safeguarding.view` over the pupil's arm. Present on the pupil detail view with a marker so a form teacher sees an asthma note without hunting. Present on the class safeguarding sheet a teacher prints before an excursion. Absent from the result sheet, the weekly report, the parent portal, the verification page and every general export. This follows section 9.9 and Appendix A entry 43, and `25-open-conflicts-to-resolve.md` item 9 records the compliance position.

The lawful basis for holding this is the parental consent captured in section I of the form, which is why 6.5.11 makes the declaration a required step rather than a formality.

### 6.5.8 Entity: pupil_document

Section H, the school-use document checklist. One row per document type per pupil, created as a set when the admission record is created so the checklist always has its rows.

| Field | Type | Req | Validation |
|---|---|---|---|
| id, pupil_id | UUID | Yes | System. |
| document_type | Enum | Yes | birth_certificate, passport_photograph, previous_school_result, transfer_letter, other. Configurable list; these five are seeded from the form. |
| other_label | String 80 | Conditional | Required where document_type is other, for the form's `Other Required Document` row. |
| received | Boolean | Yes | Defaults false. The checkbox on the form. |
| received_date | Date | No | Defaults to today when received is ticked. |
| remarks | String 200 | No | The form's Remarks column. |
| file_id | UUID | No | Optional scan or photograph of the document. |
| received_by | UUID | No | The account that ticked it. |

**Upload provision.** Each row accepts one file: PDF, JPEG or PNG, maximum 5 MB, validated by magic bytes rather than extension per 9.6, stored outside the web root and served through an endpoint applying the same privilege as the pupil record. A photograph of a birth certificate taken on a phone in the office is the expected case, so client-side downscaling applies as it does for pupil photographs.

Ticking `received` without attaching a file is allowed and normal: the school keeps paper in a folder and the tick records that the paper exists. The file is a convenience, not a requirement.

An admission can be approved with documents outstanding. The outstanding set appears on the pupil detail view and in an outstanding-documents report, because chasing a birth certificate is a term-long activity and blocking admission on it would mean the school stops using the checklist.

### 6.5.9 Entity: admission_record

Sections A, I and J. One row per pupil.

| Field | Type | Req | Validation |
|---|---|---|---|
| id, pupil_id | UUID | Yes | System. |
| session_id | UUID | Yes | The session admitted into. Defaults to the active session. |
| date_application_received | Date | No | Not in the future. |
| date_admitted | Date | Yes | Not in the future. The field the registration number's year comes from, per 6.5.10. Defaults to today. |
| class_admitted_into | UUID | Yes | A class level. The arm is chosen separately at approval, since the school often knows the level before it knows the arm. |
| admission_type | Enum | Yes | new or returning. The form's New / Returning tick. |
| admission_type_note | String 120 | No | The blank line beside the tick. |
| assessment_required | Boolean | Yes | Explicit yes or no. |
| assessment_result_remarks | Text 500 | Conditional | Required where assessment_required is true and the admission is being approved. A required assessment with no recorded outcome blocks approval, because the outcome is the reason the assessment was required. |
| assigned_class_teacher | UUID | No | An admin account. Informational at this stage; the authoritative form-teacher link is the arm's own in 6.4. |
| declaration_name | String 120 | Conditional | Section I. Required for approval. |
| declaration_signed | Boolean | Yes | Section I. A tick recording that the paper form was signed. The system captures no drawn signature: the signed paper is the artefact and this records that it exists. |
| declaration_date | Date | Conditional | Required where declaration_signed is true. |
| approved_by | UUID | No | Section J. Written at approval. |
| approved_at | Timestamp | No | Written at approval. |
| head_of_school_confirmed | Boolean | Yes | Section J's second signature block. Defaults false. |
| head_of_school_name | String 120 | No | Defaults from settings. |

### 6.5.10 Registration number

Unchanged from the previous version of this module except for **when** the number is issued: at admission approval, not at record creation. A pending record has no registration number, which is correct, because a number issued to an admission that falls through is a number burned out of a sequence the school reads as a roll.

The pattern is built in settings and defaults to abbreviation, separator, year, separator, zero-padded serial: `GRAS/2026/0041`.

| Question | Rule |
|---|---|
| Which year? | The admission year, from `admission_record.date_admitted`, not the current year. A pupil admitted in September 2026 whose approval is entered in January 2027 still gets 2026. |
| Where does the abbreviation come from? | Read from settings at the moment of issue and written into the stored string. Frozen there. Changing the abbreviation later never touches an issued number. |
| Serial width | From settings, default 4, so serials read 0001 to 9999. The counter does not wrap; it produces a five-digit serial and the number is one character longer. |
| Does the serial reset each year? | Yes by default. `serial_reset` is per_year. The alternative, continuous, is available for a school preferring one running roll number. |
| Is the number tied to a level or an arm? | No. It survives promotion, arm transfer, repeating a year and a level being renamed, unchanged. |

#### Concurrency

Two administrators approving admissions at the same counter on the same morning must not both receive `GRAS/2026/0041`. The mechanism is a counter row and a row lock, not an application-level maximum query.

1. A table `registration_counter` holds one row per counter key with columns counter_key and last_serial. Under per_year reset the key is the admission year as a string. Under continuous it is the fixed string ALL.
2. Approval runs in one database transaction. The first statement increments and returns atomically: `INSERT INTO registration_counter (counter_key, last_serial) VALUES ($1, 1) ON CONFLICT (counter_key) DO UPDATE SET last_serial = registration_counter.last_serial + 1 RETURNING last_serial`. This takes a row lock and serialises concurrent approvals on that key.
3. The returned serial is formatted and written to the pupil row in the same transaction.
4. A unique index on `pupil.registration_number` is the backstop. If it fires, the application retries the whole transaction up to three times and then fails with: Could not issue a registration number. Try again. Nothing is half-written because the counter increment and the pupil update commit together.
5. Bulk import uses the same counter, incrementing once per row inside the import transaction, so a 120-row import consumes 120 consecutive serials.

The counter is never derived from the pupil table. Counting existing pupils and adding one is the mechanism that produces duplicates, because two readers see the same count. It must not be used, including in the import path.

#### Immutability and correction

The number is immutable once issued. No ordinary edit path exists, because it appears on a pin slip, on a printed result and on a uniform tag.

- Correction requires `pupil.regnumber.correct`, Super Admin only, plus a reason of at least ten characters.
- The administrator types the new number in full. The system does not generate it, because a correction is usually to fix a wrong admission year and the corrected serial should be chosen deliberately.
- The new number must be unique against both `pupil.registration_number` and `pupil_reg_number_history`.
- The old number goes to `pupil_reg_number_history` with the reason, actor and timestamp, and stays there permanently as a lookup alias. A parent entering the old number on the portal reaches the right child and sees: This pupil's registration number is now GRAS/2026/0041. Use that number in future.
- Any pin in circulation works against the corrected number and against the historical alias. Pins reference no pupil and no number, per 6.8.2, so nothing about them changes.
- Published result sets are not rewritten. Their snapshot holds the number as printed, so the portal renders the historical number on a historical sheet and the current number on new sheets. That is correct.

A returning pupil keeps their original number. A pupil marked transferred or withdrawn who comes back is reactivated through the status screen, not re-admitted, and their number, history and old results follow them. Duplicate detection at the start of the admission flow exists to catch this.

### 6.5.11 The admission flow

This is the flow an orchestrator should read most carefully, because it is the sequence that produces a complete pupil record and its steps are not interchangeable.

The form is filled on paper by a parent and typed in by the office, or typed directly with the parent at the counter. Either way the system models it as **a resumable multi-step admission against a pending record**, not a single form submission. A parent standing at the counter without their child's birth certificate should not lose the twenty fields already typed.

**Step 1: Start and check for duplicates.** The office types surname, first name, sex and date of birth. Duplicate detection runs as the second name field loses focus, matching on surname plus first name plus date of birth, and separately on surname plus a contact phone number once one is entered. Matches show as a panel listing candidates with registration number, current arm and status, each openable. The office must tick `I have checked, this is a different pupil` before continuing. Detection never blocks outright: siblings share names, and twins share a date of birth.

Saving step 1 creates the pupil in `pending` with no registration number, and creates the empty `admission_record`, `pupil_health`, `barred_person` and `pupil_document` rows. From here the record is resumable from the admissions queue.

**Step 2: Pupil information.** The rest of section B: nationality, state, LGA, home address, previous school and class. The LGA picker filters on the state, which is the only reliable way this gets entered.

**Step 3: Parents, guardians and emergency contacts.** Sections C and D on one screen, since they are the same kind of data and a `copy from father` action across them saves real retyping. At least one of father, mother or guardian, and the primary emergency contact, are required to leave this step. One contact is marked primary.

**Step 4: Collection and safeguarding.** Section E. The authorised-pickup list, and the explicit barred-persons question. The barred question must be answered yes or no; it cannot be skipped, because an unanswered question here is indistinguishable from a No and they are not the same thing.

**Step 5: Health and safety.** Section F. The three yes-or-no questions must be answered explicitly, with details where any is Yes. Preferred hospital and phone are prompted with a visible note that a blank here is a blank in an emergency, but are not blocking.

**Step 6: Other information.** Section G, one free-text box. Skippable in one tap.

**Step 7: Documents.** Section H. The office ticks what it has received and optionally attaches scans. Fully skippable and resumable later.

**Step 8: Declaration.** Section I. The declaring parent's name, the date, and a tick recording that the signed paper form exists. Required.

**Step 9: Review and approve.** A single read-only summary of everything, grouped by form section, with each incomplete required item shown as a link straight to its step. The approve action requires `pupil.admission.approve` and asks for:

- The destination **arm**, from the level recorded in section A. The selector lists the level's arms for the active session, each showing enrolled of capacity. Capacity is checked per 6.4.6.
- The **assessment outcome**, where section A said an assessment was required.
- Section J's approver, which is the acting account, and the head-of-school confirmation tick.

Approval, in one transaction: sets `status` to active, issues the registration number per 6.5.10, writes the section J fields, and opens an enrolment in the chosen arm effective from the admission date or the session start date, whichever is later.

The confirmation screen shows the issued registration number in large type with a copy button and a `Print admission slip` action, because the office will be asked for the number immediately.

**Blocking conditions.** Approval is blocked when no arm exists for the level in the active session, when no term is active (`No term is currently active. Open a term before approving admissions.`), when any required item across steps 1 to 8 is missing, or when a required assessment has no recorded outcome. Each block names the step and links to it.

### 6.5.12 Completeness, and the difference between blocking and chasing

Two distinct ideas, and conflating them is how a school ends up unable to admit a child over a missing birth certificate.

**Required for approval**, meaning the record cannot become active without it: names, sex, date of birth, nationality, state, LGA, home address, one parent or guardian with a phone number, the primary emergency contact, an explicit answer to the barred-persons question, explicit answers to the three health questions, the declaration, the destination arm, and the assessment outcome where one was required.

**Chased, not blocked**, meaning the record goes active and the gap is tracked: photograph, previous school details, the authorised-pickup list, preferred hospital, blood group, genotype, special instructions, section G, and every document in the section H checklist.

A **record completeness indicator** appears on the pupil detail view and as a list column: a percentage across the chased set, with a hover listing what is missing. An `Incomplete records` report drives the office's chasing, and it is the report a head teacher opens in week three of a session to find the forty children with no photograph and the six with no emergency contact number.

### 6.5.13 Bulk import

The school will type its existing register in once and import a new intake each September. The import path is forgiving about formatting and unforgiving about ambiguity.

- The administrator downloads a template: XLSX, a header row, one data sheet, and a second sheet listing accepted values for sex, state, relationship, blood group, genotype and the arm names currently available. Providing the arm list is what stops half the rows failing on a mistyped class.
- **Columns**, extended for the admission form: Surname, First Name, Middle Name, Sex, Date of Birth, Nationality, State of Origin, LGA, Home Address, Previous School, Previous Class, Admission Date, Admission Type, Class Level, Arm Label, Father Name, Father Phone, Father WhatsApp, Father Occupation, Mother Name, Mother Phone, Mother WhatsApp, Mother Occupation, Guardian Name, Guardian Relationship, Guardian Phone, Primary Contact, Emergency Primary Name, Emergency Primary Relationship, Emergency Primary Phone, Emergency Alternate Name, Emergency Alternate Relationship, Emergency Alternate Phone, Has Allergy, Allergy Details, Has Medical Condition, Medical Condition Details, Takes Medication, Medication Details, Preferred Hospital, Hospital Phone, Blood Group, Genotype.
- Health columns are **optional in the file**, and a row without them imports with the health questions unanswered rather than answered No. The distinction from 6.5.7 holds through the import path, and those pupils appear on the incomplete-records report.
- The authorised-pickup list and the barred-persons details are **not importable**. They are per-child free-form lists that do not survive a spreadsheet column, and the barred field is too sensitive to arrive in a bulk paste. Both are entered per pupil afterwards.
- Imported pupils are created **active with a registration number**, not pending, because an existing register is not a queue of applications. `admission_record` rows are created with `declaration_signed` false and flagged for the office.
- The target arm is two columns, Class Level and Arm Label, matched case-insensitively for the active session. A single column holding `Primary 2C` is also accepted and parsed against composed display names, because that is what people type whatever the template says.
- Dates accept DD/MM/YYYY, YYYY-MM-DD, or an Excel date serial. A two-digit year is rejected rather than guessed.
- Upload runs a validation pass and returns a report without writing: rows accepted, rows rejected, and for each rejection the sheet row, the column and the reason. Displayed on screen, downloadable as CSV.
- Duplicate detection runs across the file and against the existing register. Duplicates inside the file are rejected. Matches against the register are warnings, and the administrator chooses per row to skip or create.
- **Commit is all or nothing.** A file with 118 valid and 2 invalid rows imports nothing until the two are fixed, per Appendix A entry 22.
- Capacity is checked per arm across the whole file. Over capacity is a warning requiring `arm.capacity.override` to commit, not a rejection.
- Registration numbers are issued in file order, so sorting the spreadsheet first gives the roll order the school wants.

### 6.5.14 Status transitions

| From | To | Effect and rules |
|---|---|---|
| pending | active | Admission approval, per 6.5.11. Issues the registration number and opens the first enrolment. The only route into active for a new record. |
| pending | withdrawn | The application lapsed or was declined. Requires a reason. No registration number is ever issued, so no serial is consumed. The record is retained rather than deleted, because a family that reapplies next year should be findable. |
| active | transferred | The pupil has left for another school. Closes the open enrolment on the effective date. Requires a reason. Published results stay published and readable, because a parent is entitled to the result of a term their child sat. |
| active | withdrawn | Left without transferring, or withdrawn by the school. Same effects. The distinction is kept because the school reports on them differently. |
| active | graduated | Set by promotion at the terminal level, or manually. Closes the enrolment at the session end date. |
| transferred or withdrawn | active | Reactivation. Requires `pupil.status.update` and a destination arm. Opens a new enrolment. Keeps the original registration number and all history. |
| graduated | active | Permitted, for a pupil repeating the terminal level after all. Requires a reason. |

A **pending** pupil is excluded from every arm roster, every enrolment count, every capacity calculation, every score sheet, every result set, every weekly report, every report and every portal lookup. It exists in the admissions queue and nowhere else.

An inactive pupil of any kind is excluded from score entry, the completeness gate, class averages, highest and lowest, and position ranking, from the effective date onward. A pupil withdrawn in week ten of a twelve-week term does not appear on that term's result set. Where the school wants a result for a pupil who left before the examination, the head teacher keeps them active until the result set is published and withdraws them afterwards. The withdrawal screen says so.

### 6.5.15 List view, detail view and search

**List columns:** photograph thumbnail, registration number, full name surname-first, sex, age, class as the composed arm display name, status, completeness percentage. Filters: level, arm, session, status, sex, completeness below a threshold, outstanding documents. Default sort is class in progression order then surname ascending, which is the order a school reads a register in. Page size 50.

**A separate Admissions queue view** lists pending records only, with columns for the applicant's name, level applied for, date application received, the step the record last reached, and what is missing. This is the office's work list and it should not be mixed into the main register.

**Search** matches, in one box: any part of surname, first name or middle name; the registration number in full or its serial alone, so typing 41 finds `GRAS/2026/0041`; any contact's phone number in either format; any contact's name; and an authorised pickup person's name, which is the search a gate officer runs. Scope-aware, so a Class Teacher finds only their own arms. Results show which field matched.

**Detail view**, grouped to mirror the form so the office can check a screen against a paper page:

- Identity block: photograph, name, registration number with a copy button, age, sex, current arm with a route to it, status, completeness indicator.
- Pupil information: section B fields.
- Contacts: father, mother, guardian and both emergency contacts, phone numbers as tap-to-call links, the primary contact marked.
- Collection: the authorised-pickup list, and the barred-persons block behind `pupil.safeguarding.view`, with a visible marker that restricted content exists rather than a silent omission.
- Health and safety: section F behind the same privilege, with an alert-styled marker in the identity block when an allergy, condition or medication is recorded, so a form teacher sees it without hunting.
- Other information: section G.
- Documents: the checklist with received state, dates, remarks and any attached files.
- Admission record: sections A, I and J, including who approved and when.
- Enrolment history: a dated list of arms. This is how a dispute about a position gets settled.
- Result history: one row per term per session with state and a route to the sheet.
- Weekly reports: one row per term with the count of published weeks, per 6.10.
- Portal access history: every viewing session opened against this pupil with the pin prefix, batch, date and truncated source address. This is the screen that answers who has been looking at this child.
- Audit trail for the record.

### 6.5.16 Error and edge cases

| Case | Behaviour |
|---|---|
| Admission approval attempted with no active term | Blocked, per 6.5.11. |
| Two pupils with identical names and dates of birth | Allowed after the confirmation tick. Twins are real. |
| Contact phone shared by four pupils | Allowed and expected. Four siblings share a father's number. |
| Father deceased or absent, mother only | Fine. One of the three responsible-adult roles is enough. No screen requires a father. |
| Both parents absent, an aunt is raising the child | Recorded as the guardian role with relationship Aunt, marked primary. |
| Emergency contact is the same person as the mother | The `copy from mother` action fills it. Two rows are stored. |
| Parent answers No to all three health questions | Stored as three explicit falses. This is a complete health section, not an empty one, and the record counts as complete. |
| Parent declines to answer the health questions | The step cannot be completed, so the record stays pending. The office records what the parent said in section G and escalates to the head teacher, who can approve with a reason. |
| Barred-persons question answered Yes with no name | Rejected: You answered Yes. Enter the name of the person who should not collect this child. |
| Photograph upload of an 8 MB phone image | Rejected at 3 MB: This photograph is 8 MB. The limit is 3 MB. Reduce the size or take the photograph again at a lower quality. Client-side downscaling is specified in 9.6. |
| Date of birth making the pupil 19 in Primary 3 | Accepted with a warning, not blocked. Over-age enrolment happens. |
| Pupil transferred out mid-term with marks entered | Marks retained. The pupil drops off the result set from the effective date and can be reinstated if the withdrawal is reversed. |
| Pending record abandoned for a whole term | Stays pending. The admissions queue sorts by age and flags records untouched for 30 days. Nothing is auto-deleted, because a family that returns in January should not start again. |
| Import file with a level that has no arm | Row rejected: Row 34: Primary 6 has no arm named A in 2026/2027. Create the arm or correct the row. |
| Import file with 3000 rows | Accepted. Runs as a background job with progress, validation report before commit as usual. The interface warns above 500 rows that the import may take a minute. |
| Pupil deleted | Only where no result, no pin use, no enrolment history and no issued registration number exists, which in practice means a pending record created in error. A soft delete: the row is marked and excluded everywhere. An approved pupil is never deleted, only status-changed. |

### 6.5.17 Endpoints

| Endpoint | Notes |
|---|---|
| GET /pupils | Paged, filtered, scope-aware search. Excludes pending unless status=pending is passed. |
| GET /admissions | The pending queue with per-record completeness and last step reached. |
| POST /admissions | Step 1. Creates the pending pupil and its empty child rows in one transaction. |
| PATCH /admissions/{id} | Steps 2 to 8. Accepts a partial payload per step, so a half-finished step still saves. |
| GET /admissions/{id}/completeness | What is missing, split into blocking and chased, each keyed to its step. |
| POST /admissions/{id}/approve | Destination arm, assessment outcome, section J fields. Issues the number, opens the enrolment, sets status active. Idempotency key required so a retry cannot issue two numbers. |
| POST /admissions/{id}/decline | Reason. Sets status withdrawn without issuing a number. |
| GET /pupils/{id} | Full detail. Health and barred-persons blocks omitted unless the caller holds `pupil.safeguarding.view`, and their omission is signalled rather than silent. |
| PATCH /pupils/{id} | Biographical fields. Rejects any attempt to set registration_number with 409. |
| GET, PUT /pupils/{id}/health | Requires the safeguarding privileges. Every GET writes an audit event. |
| GET, PUT /pupils/{id}/pickup-persons | The authorised list. |
| GET, PUT /pupils/{id}/barred-persons | Requires the safeguarding privileges. Audited on read. |
| GET /pupils/{id}/documents | The checklist with received state and file references. |
| POST /pupils/{id}/documents/{type} | Tick received, set remarks, optionally attach a file. Multipart. |
| POST /pupils/{id}/photo | Multipart upload. Returns derivative URLs. |
| POST /pupils/{id}/status | Target status, effective date, reason. |
| POST /pupils/{id}/transfer | Destination arm, effective date. Returns publication and recompute consequences when dry_run is set. |
| POST /pupils/{id}/registration-number | Correction. Super Admin, reason required. |
| GET /pupils/duplicates?surname=&first_name=&dob=&contact_phone= | Duplicate candidates for step 1. |
| GET, POST /pupils/{id}/contacts, PATCH /contacts/{id} | Contact management across all five roles. |
| GET /pupils/import/template | XLSX download, populated with the current arm and state lists. |
| POST /pupils/import/validate | Returns the validation report. Writes nothing. |
| POST /pupils/import/commit | All or nothing. |
| GET /reports/incomplete-records | The chasing report described in 6.5.12. |
| GET /reports/safeguarding?arm_id= | Health and collection data for one arm, for an excursion or a gate list. Requires `pupil.safeguarding.view`. Audited, and the only export these fields ever appear in. |
