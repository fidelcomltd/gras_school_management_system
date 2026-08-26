# 10. Reporting Requirements

Reports are read-only views over data the system already holds. Every report honours the scope rules, so a Class Teacher running the broadsheet gets her arm. Every report offers CSV and PDF output under `report.export`, and every export writes an audit event naming the report, the filters and the row count.

| Report | Privilege | Contents, filters and output |
| --- | --- | --- |
| Arm broadsheet | `report.view`, arm-scoped | One row per pupil, one column group per subject showing continuous assessment total, examination and total, then total obtained, average, arm position and level position. Filters: session, term, arm. Sorted by arm position ascending. This is the report the head teacher approves from and the one the school will print most. Printed A4 in the wide orientation. |
| Grade distribution | `report.view` | Counts and percentages of pupils in each grade band, per subject and overall, for one arm or one level. Filters: session, term, level, arm. Shown as a table with a plain bar column rendered in text characters so it survives monochrome photocopying. |
| Subject performance | `report.view` | One row per subject: class average, highest, lowest, number counted, number of absentees, number passing, pass rate. Comparable across the arms of a level in one table, which is how a head teacher sees that 3B's Mathematics teaching is behind 3A's. Filters: session, term, level. |
| Merit list | `report.view` | Position order for an arm or a level, showing position, name, registration number, average and grade. Used for prize-giving. Filters: session, term, arm or level, top N. Prints on A4 portrait in two columns. |
| Annual cumulative report | `report.view` | One row per pupil: three term averages, cumulative average, cumulative grade, annual position, promotion status. Filters: session, arm, level. Available once annual computation has run. |
| Promotion list | `report.view` | Per pupil: current arm, annual average, core subject results, proposed outcome, final outcome, target arm, and the reason where an override was recorded. Filters: session, level, outcome. This is the document the school files. |
| Nominal roll | `report.view`, arm-scoped | The pupil register for an arm or a level: registration number, full name, sex, date of birth, age, admission date, status, primary guardian name and phone. Filters: session, level, arm, status, sex. The register the school takes to any inspection. |
| Enrolment summary | `report.view` | Counts by level and arm, split by sex, with capacity and remaining space. One row per arm, subtotals per level, a school total. Filters: session, status. Answers the proprietor's question about how full the school is. |
| Result entry progress | `report.view` | One row per arm for the active term: subjects mapped, mark cells complete of total, traits complete, attendance complete, remarks complete, result set state. Filters: session, term, level, state. This is the report that runs at the end of week eleven and tells the head teacher which four teachers to chase. |
| Pin distribution and usage | `pin.usage.view` | One row per arm: pupils, pins issued, pins used at least once, pins exhausted, pins revoked, date of last use. Plus a drill-down to the per-pin report in 6.8.11. Filters: session, arm, batch, state. Answers whether parents actually received their slips. |
| Pupil cumulative record | `report.view`, arm-scoped | One pupil across every session at the school: term by term averages, positions, promotion outcomes, and the arms sat in. Two pages at most. This is what a school produces when a former pupil asks for a record, and producing it by hand from twelve result sheets is the job this report removes. |
| Guardian contact list | `guardian.view`, arm-scoped | Per arm: pupil name, primary guardian name, relationship, phone, alternate phone. Deliberately narrow. It carries no address, no occupation and no second guardian, because the use case is a form teacher telephoning parents and a wider export of guardian data leaving the building is what the retention rules in 9.9 exist to limit. Export requires `report.export` and is audited. |
| Audit report | `audit.view` | The filtered audit log from 6.1.12 rendered for reading and export: date and time, actor, action, entity, outcome, reason, and the before and after values for score changes. Filters: date range, actor, action, entity type, outcome. The report the school opens when a mark is disputed. |
| Settings change history | `audit.view` | Every configuration version with its actor, timestamp, reason and a plain-language summary of what changed, for example Grading band C lower bound changed from 60 to 58. Filters: date range, settings group. This is how the school answers why a grade looks different from last year. |

Two reports are explicitly not built, because they would be misread. There is no cross-arm pupil ranking presented without its cohort labels, since 8.4.7 exists precisely to prevent that. And there is no teacher performance report, because attributing a class average to a form teacher when a subject may be taught by somebody else, and when arm composition is not controlled, produces a number that will be used in employment decisions and cannot support them.


---

---

## 10.2 Amendment: additional reports

Added alongside the weekly report module and the expanded pupil record. Each follows the conventions above: scope-aware, CSV and PDF output under `report.export`, every export audited with the report name, filters and row count.

| Report | Privilege | Contents, filters and output |
|---|---|---|
| Weekly report completion | `report.view` | One row per arm per week for the active term: pupils with any note, cells filled against cells available, published state, last edited by and when. The report a head teacher opens on a Monday to see which teachers wrote nothing last week. Filters: session, term, arm, week, published state. |
| Illness observation summary | `report.view` plus `pupil.safeguarding.view` | One row per pupil per term where `symptoms_of_illness` was recorded on two or more days, with the dates and the text. Restricted because it is health observation about a child. Not present in any general export. |
| Incomplete pupil records | `report.view` | One row per pupil with a completeness percentage and the specific missing items from the chased set in 6.5.12. Filters: level, arm, status, completeness threshold. Drives the office's chasing in the first weeks of a session. |
| Outstanding admission documents | `report.view` | One row per pupil per unreceived document type from the section H checklist, with the date the record was created so the oldest gaps sort first. Filters: level, arm, document type. |
| Admissions pipeline | `report.view` | Pending records by level with the step each last reached, days since creation, and what is blocking approval. Filters: session, level, age of record. |
| Class safeguarding sheet | `pupil.safeguarding.view` | Per arm: pupil name, photograph, allergies, medical conditions, medication, special instructions, preferred hospital and phone, authorised pickup persons, and a barred-persons marker without the detail. Printed before an excursion and kept at the gate. **The only export in the product carrying health data**, audited on every generation, and excluded from every other report by design. |
| Development domain summary | `report.view` | Nursery only. Per arm per term, the count of pupils at each of the four rating points for each indicator, so a head teacher can see that nine children in Nursery 2A are not yet potty trained without reading thirty sheets. |
| Fee notice audit | `report.view` | Per level per term, the configured fee lines and their amounts, plus the count of pupils carrying an outstanding figure and its total. Confirms what was printed on the sheets before they went home. |
