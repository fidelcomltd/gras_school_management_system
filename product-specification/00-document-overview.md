# GRAS School Management System: Document Overview

> Source sections 1 to 3 of the full specification. Title page and table of contents are omitted; they carry no build information.

# 1. Document Purpose and Intended Readers

This document specifies the software Golden Royal Ark School will run to manage its pupils, its classes and its terminal results. It is written to be built from directly. A backend engineer should be able to derive the schema and the endpoints from it, a mobile or web engineer should be able to lay out every screen from the field tables, and a QA tester should be able to write test cases from the validation rules and error cases without asking anyone what was meant.

The school told us plainly that it does not know what a standard Nigerian primary result sheet must contain, and asked us to supply that knowledge rather than ask for it. Section 8, Appendix C and Appendix D of this document carry that domain knowledge. Where the school had no view and the answer had to be settled to make the product buildable, I settled it, wrote it in as a firm requirement, and recorded the call in Appendix A with my reason. Appendix B holds the short list of things the school genuinely has to answer, each with a proposed default so that silence still produces working software.

## 1.1 Who should read what

| Reader | Sections that matter most |
| --- | --- |
| Head teacher and proprietor | Sections 3, 6, 7 and 8 and Appendix B. Section 8 shows exactly how a position in class is arrived at, which is the number parents argue about. |
| Backend engineer | Sections 4, 5, 6 and 9. Every module section ends with the endpoints it implies. Section 6.2 on configuration binding changes the schema, so read it before writing migrations. |
| Frontend and mobile engineer | Section 6 field tables, section 7 flows, section 6.9 for the parent portal, Appendix C for the result sheet data contract. |
| QA tester | Every module section has an error and edge case block. Section 8 has a worked numeric example that doubles as a regression fixture. Section 9 has the performance targets. |
| Whoever administers the system day to day | Section 6.2, in particular the first-run checklist, and section 6.8 on pins. |

## 1.2 Conventions used

- Field tables give the field name, its type, whether it is required, and its validation rule. Required means the create form will not submit without it.
- `module.action` names, for example `result.approve`, refer to privileges from the register in section 4.4.
- Dates display as DD/MM/YYYY throughout the interface and on every printed artefact. Times display in West Africa Time, UTC+1.
- A session is written as 2026/2027. A term is First Term, Second Term or Third Term.
- Where this document says class level it means a year group, for example Primary 2. Where it says arm it means one physical class of pupils within that year group, for example Primary 2A. The word class on its own is not used in a normative sentence anywhere in this document, because it is ambiguous.


---

# 2. Product Overview and Objectives

Golden Royal Ark School runs nursery and primary classes on a three-term Nigerian session. At the end of each term the school produces a terminal result sheet for every pupil, computes each pupil's position in class, and issues that result to parents. Today that work is done by hand, and the head teacher spends the last week of every term adding columns of marks and ranking pupils on paper. The system replaces that work and the paper distribution of results that follows it.

The product has two faces. The first is an administrative back office where the school configures itself, keeps its pupil register, records marks, computes and approves results, and generates the pins parents use. The second is a single public page where a parent enters a registration number and a pin and reads or downloads their child's result. Parents get no accounts, no passwords and no app. That is deliberate. A school of this size cannot support a parent password reset queue, and the pin slip is an artefact the school already knows how to hand across a counter.

## 2.1 Objectives

1. Remove hand computation from the terminal result. Once marks are entered, totals, grades, subject positions, class averages, highest and lowest scores and overall position are computed by the system and are arithmetically identical every time.
2. Make the school's own rules editable by the school. Grading bands, continuous assessment weighting, trait lists, the rating scale and the registration number prefix are settings, not code. The school changes its grading scale without a developer.
3. Bind every published result to the configuration that produced it, so a result read in 2029 shows the grades it showed in 2026.
4. Let the school add an arm the week it needs one. Three arms of Primary 1 and one arm of Primary 6 in the same session is a normal configuration.
5. Get a result into a parent's hand on a low-end Android phone over a weak connection, in under ten seconds from the moment they press the button.
6. Keep an audit trail good enough to answer the question the school will eventually be asked: who changed this mark, and when.

## 2.2 What success looks like at the end of a term

The class teacher of Primary 3B enters four columns of marks per subject for twenty-nine pupils. The system tells her which cells are still empty. She submits. The head teacher opens the computed sheet for the whole arm, reads the class averages, writes his remarks, and approves. The bursar prints twenty-nine pin slips already bound to twenty-nine registration numbers. Nobody adds anything up. No parent telephones the school to ask what their child's position was.


---

# 3. Scope

## 3.1 In scope

| No. | Module | What it covers |
| --- | --- | --- |
| 1 | Admin accounts, roles and privileges | Administrative user accounts, roles built by composing privileges, arm-scoped assignment, account states, audit log. |
| 2 | School settings and academic configuration | School identity, registration number pattern, grading scale editor, assessment structure, trait configuration, result rules, pin defaults, configuration versioning. |
| 3 | Academic sessions and terms | Session creation, three terms per session, term states, end-of-session promotion. |
| 4 | Class levels, arms and classroom management | Class levels as editable records with a progression chain. Arms created on demand per level per session. Form teacher assignment, capacity, rosters. |
| 5 | Pupil management | Pupil records, guardian records, registration number issue, enrolment into an arm, transfers, bulk import, search. |
| 6 | Subject management | Subjects, mapping to a level for a session and term, per-arm exceptions. |
| 7 | Result recording, computation, approval and publication | Score entry per arm per subject per term, completeness gate, computation of totals, grades, positions and class statistics, non-academic input, approval, publication, annual cumulative result. |
| 8 | Access pin management | Pin batches bound to pupils, generation, printing, revocation, usage reporting. |
| 9 | Public parent result-checking portal | Unauthenticated lookup by registration number and pin, term selection, result view, PDF download, verification. |

## 3.2 Out of scope

The following are not part of this product. They are listed so that they are not raised as omissions during build or acceptance. Adding any of them is a change request against a new version of this document.

| Not included | Note |
| --- | --- |
| Fee and payment processing | No invoices, no receipts, no fee balances, no payment gateway. A result is never withheld by the system for non-payment. If the school wants that, it withholds the pin slip. |
| Staff HR and payroll | Admin accounts exist only so that people can operate the system. There is no employment record, no salary, no leave. |
| Timetabling | No periods, no rooms, no teacher allocation beyond one form teacher per arm. |
| Attendance as a standalone module | There is no daily register. Attendance enters the system as three numbers per pupil per term on the result input screen: times school opened, times present, times absent. |
| Messaging, SMS and email broadcasts | The system sends no messages to parents. Guardian phone numbers are stored for the school's own use, not for automated sending. |
| Library, transport, admissions and applications | None of these exist in the product. |
| CBT and online examinations | Pupils never log in. Examinations happen on paper and marks are typed in. |
| Parent mobile app, parent accounts, parent logins, parent password reset | Parents are anonymous holders of a pin. There is no parent identity in the data model. |


---
