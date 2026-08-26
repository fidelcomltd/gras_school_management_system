# Appendix D. Glossary

These are the terms used in this document as the school uses them. I have written them out because a developer who has not built for a Nigerian school will read arm, term and session as loose synonyms for class and time, and the whole data model turns on their not being that.

| Term | Meaning in this document |
| --- | --- |
| Academic session | The school year, written as two calendar years joined by a slash: 2026/2027. It runs from about September to about July and contains exactly three terms. Not a login session, which this document always calls a sign-in session. |
| Term | One of the three teaching periods in a session, named First Term, Second Term and Third Term. Each has a start date, an end date and its own set of results. Roughly thirteen weeks. |
| Class level | A stage of schooling: Nursery 1, Nursery 2, Nursery 3, Primary 1 through Primary 6. The level is the curriculum stage and carries no notion of which room a child sits in. |
| Arm | A subdivision of a level, also called a stream. Where Primary 3 has too many pupils for one room the school runs Primary 3A and Primary 3B. An arm has a form teacher, its own register, its own marks and its own class positions. In this system an arm belongs to one session, per Appendix A entry 14. |
| Class | In everyday school speech, the level and the arm together: Primary 3A. Used in this document only where the composed display name is meant. |
| Continuous assessment, C.A. | The marks a pupil earns during the term from tests and classwork rather than from the terminal examination. Here it is First C.A. out of 15, Second C.A. out of 15 and Assignment out of 10, adding to 40. |
| Examination | The end-of-term paper, worth 60 in this school. Together with the continuous assessment it makes 100 per subject. |
| Form teacher, class teacher | The member of staff responsible for one arm: its register, its attendance, its traits and its remarks. In this system a Class Teacher is a role assigned over named arms rather than a fixed account type. |
| Head teacher | The senior academic officer, who approves results before they are issued and signs the sheets. In this system the account that moves a result set from Awaiting Approval to Approved. |
| Proprietor | The owner of the school. In a private school of this size the proprietor is usually the person who signs off on software and often holds a Super Admin account. |
| Bursar | The member of staff who handles school money. Fees are out of scope here, so the seeded Bursar role holds read privileges only and exists so that the school does not give the bursar a teacher's account. |
| Records clerk | The office staff member who registers pupils, types guardian details and prints slips. Usually working on a shared office computer, which is why Appendix A entry 18 keeps that machine on a low-privilege role. |
| Position in class | A pupil's rank within their arm by total marks, printed as an ordinal. The figure parents look at first. Distinct from the level position, which ranks a pupil against every arm of the level. |
| Broadsheet | One page holding every pupil in an arm against every subject, used by the head teacher to check and approve a term's marks and kept as the school's master record. Printed A4 in the wide orientation, per 9.10. |
| Affective domain | Behaviour and attitude: punctuality, neatness, politeness, honesty. Rated rather than scored, and printed as its own block on the sheet. |
| Psychomotor domain | Physical and practical skills: handwriting, drawing, sports, handling of tools. Rated on the same scale as the affective traits and printed as its own block. |
| Cumulative average, annual average | A pupil's average across the terms of a session, used for the annual result and for promotion. Computed here as the plain average of the term averages by default, per Appendix A entry 19. |
| Core subjects | The subjects a pupil must pass to be promoted regardless of the overall average, typically English Language and Mathematics. Appendix B question 4 asks the school to name them. |
| Promoted, repeat | The end-of-session outcome. A promoted pupil moves to the next level, a repeating pupil sits the same level again. Decided by the school reviewing a proposed batch, per Appendix A entry 18. |
| Resumption | The day a term begins and pupils return. Printed on the sheet as the date the next term begins, which is the line parents check before anything else. |
| Terminal report sheet | The document issued to a parent at the end of a term, carrying the subject marks, the summary, the traits, the attendance and the remarks. What Appendix C is a contract for. |
| Registration number, admission number | The permanent identifier the school gives a pupil at admission, here in the form GRAS/2026/0041. Written on the child's file and books and used by a parent on the portal alongside their pin. |
| Access pin | The use-counted code on a printed slip that lets somebody read published results on the portal without an account. It is not tied to a pupil: presented with any registration number it opens that pupil's results, and each lookup spends one of its uses. Specified in 6.8, with the reasoning and the risk in 6.8.2 and Appendix A entry 35. |
| NDPA, NDPC | The Nigeria Data Protection Act 2023, the law governing personal data in Nigeria, and the Nigeria Data Protection Commission, the regulator that enforces it and receives breach reports. Obligations are in 9.9. |
| WAT | West Africa Time, UTC plus one hour, with no daylight saving. Every time this system displays is in WAT and every time it stores is in UTC. |
| Basic education | The nine-year span of primary and junior secondary schooling in Nigeria. This school covers the nursery and primary part of it and its pupils leave after Primary 6. |
| First School Leaving Certificate | The certificate awarded at the end of Primary 6. Out of scope here, and the reason Primary 6 is the terminal level with no next level, per 6.4.3. |

This is the end of the specification. Appendix B holds the questions that remain, and question 1 is the one I would like answered first.
