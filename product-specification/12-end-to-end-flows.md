# 7. End-to-End Flows

Five flows, each written as the sequence of actions a real person performs, with the actor named at each step. These are the paths QA should walk before anything else, and they are the paths that decide whether the school adopts the product or quietly goes back to paper.

## 7.1 First-run school setup

Actor throughout: the bootstrap Super Admin, who at Golden Royal Ark School will be the proprietor or the head teacher.

1. The installer runs the seed command with the proprietor's email address and reads the generated password off the console.
2. The proprietor logs in and is forced through a password change. No other screen is reachable until it is done.
3. The dashboard shows the eighteen-step setup checklist from 6.2.2, with step 1 ticked. Every other step shows its blocking dependency.
4. School identity: name Golden Royal Ark School, short name Golden Royal Ark, address, phone, email, motto. Logo uploaded. Head teacher name and signature image uploaded.
5. Abbreviation confirmed as GRAS. The registration number preview reads GRAS/2026/0001, since no pupil exists yet. Serial width left at 4, separator left at /, reset left at per year.
6. Grading scale opened. The six seeded bands are already correct for this school, so nothing is changed. The live preview beside the table shows what a parent will see.
7. Assessment structure opened. The school uses 15, 15, 10 and 60, which is the seed, so nothing is changed.
8. Trait lists reviewed. The school adds Cooperation to the affective list, making eight affective traits. The psychomotor list is left as seeded.
9. Result rules set: simple average, arm-scoped position, level position shown, shared-position tie-breaking, pass mark 40, promotion threshold 40, require core pass on. Core subjects cannot be chosen yet because no subject exists, so the field shows Set this after you create subjects and the checklist step stays amber.
10. Class levels reviewed. Golden Royal Ark runs a nursery, so all nine seeded levels stay active and nothing is edited.
11. Session created: 2026/2027, starting 14/09/2026, ending 23/07/2027. First Term 14/09/2026 to 18/12/2026 with resumption 11/01/2027. Second Term 11/01/2027 to 09/04/2027 with resumption 26/04/2027. Third Term 26/04/2027 to 23/07/2027 with resumption 13/09/2027. Times school opened left blank on all three.
12. First Term opened. It fails on the first attempt: No arms exist for 2026/2027. Create at least one arm before opening a term. The proprietor follows the link.
13. Bulk arm creation run. Nine active levels, one arm each except Primary 1 which gets three and Primary 2 which gets two, so twelve arms. Capacities set. The preview lists Nursery 1A through Primary 6A with composed display names. Committed.
14. First Term opened successfully. The session moves to active.
15. Admin accounts created for the head teacher, the bursar and eleven form teachers. Each temporary password is written down and handed over.
16. Class Teacher role assigned to each form teacher, scoped to their arm, for session 2026/2027. The head teacher gets Head Teacher school-wide. The bursar gets Bursar school-wide.
17. Subjects created. The proprietor clicks nine suggestions from the list in 6.6.2 and edits two codes.
18. Result rules revisited to set core subjects to English Studies and Mathematics. The checklist step goes green.
19. Subject mappings set on the grid for First Term: nine subjects across the six primary levels, five subjects across the three nursery levels. Saved in one action.
20. Pupils imported from the school's existing spreadsheet. The template is downloaded, the school's data pasted in, the file validated, eleven rows corrected, the file committed. Four hundred and twelve pupils registered, each issued a number and enrolled into an arm.
21. The checklist is complete and disappears from the dashboard. Score entry is now reachable.

Elapsed time for a prepared administrator, excluding the spreadsheet work: under ninety minutes. The spreadsheet is the long part and always will be.

## 7.2 Pupil registration to first result

1. A parent arrives in week three of First Term. The School Administrator opens Register pupil.
2. Names, sex, date of birth 04/03/2018, admission date 05/10/2026 entered. Age shows as 8.
3. Duplicate detection finds nothing.
4. Arm chosen: Primary 3A, showing 27 of 30 enrolled. No capacity warning.
5. Guardian entered: mother, phone, address, marked primary.
6. Save. The system issues GRAS/2026/0219, creates the pupil, the guardian and an enrolment in Primary 3A effective 05/10/2026, in one transaction.
7. The admission slip is printed with the registration number.
8. The form teacher of Primary 3A opens her score sheet for English Studies. The new pupil appears in the list, in surname order, with empty cells. Nothing had to be refreshed or reassigned.
9. The readiness grid for Primary 3A now shows 28 pupils and the completeness counters move from 27 of 27 to 27 of 28 on every subject, which is exactly the signal the teacher needs.
10. The teacher enters the new pupil's marks for the two continuous assessment tests already sat. The assignment mark and the examination follow later in the term.
11. At the end of term the pupil is in the completeness gate, in the class average, in the highest and lowest, and in the position ranking, on the same footing as everybody else.
12. No pin action is needed. Pins are unbound, so a pupil joining after a batch was printed changes nothing: the parent is handed a slip from the same stock as everybody else.

## 7.3 Score entry to published result

Primary 3A, First Term 2026/2027, twenty-eight pupils, nine subjects.

1. Week eleven. The form teacher opens the score sheet for English Studies, First Term, Primary 3A. Columns are CA1 / 15, CA2 / 15, ASG / 10, EXAM / 60, Total.
2. She enters CA1 down the column for all twenty-eight pupils, tabbing downward. Automatic save fires twice while she works. She presses Save draft and moves to CA2.
3. The result set for Primary 3A First Term is created on her first save, in state Draft, with needs_recompute true.
4. She repeats for the other eight subjects across the following days. One pupil, Musa Ibrahim, was absent for the Mathematics examination, so she ticks Abs in his examination cell rather than typing 0.
5. She opens the trait grids and rates eight affective and five psychomotor traits for all twenty-eight pupils, using Fill column and then correcting the exceptions.
6. She enters attendance. Times school opened for First Term is 58, taken from the term record. For each pupil she enters times present, and the absent figure is validated against 58.
7. She writes the class teacher's remark for each pupil, using three saved templates for twenty-two of them and writing individually for six.
8. The readiness grid shows all four counters complete. She presses Compute. The system writes the computed tables and clears needs_recompute.
9. She presses Submit. Preconditions pass: gate complete, computation current, form teacher assigned, times school opened set. The set moves to Awaiting Approval and her marks become read-only.
10. The head teacher's queue shows Primary 3A among eleven arms awaiting approval. He opens the broadsheet.
11. The system flags one row: Mathematics has a class average of 59.7 against a school average of 68 across other subjects. He checks it against the mark book, decides the paper was hard, and accepts it.
12. He writes the head teacher's remark for each pupil, filling one phrase down the column and editing nine.
13. He presses Approve. The set moves to Approved.
14. He presses Publish. The system checks the preconditions: every head teacher remark present, logo and signature present, resumption date 11/01/2027 set, times school opened 58 set. All pass.
15. The configuration snapshot is written: grading scale, assessment structure, trait lists including the added Cooperation trait, trait scale, result rules, school name, motto, logo reference, head teacher name and signature reference, level name Primary 3 and arm display name Primary 3A. State moves to Published, revision_number becomes 1.
16. The twenty-eight pupils' results become readable to anybody holding a valid pin and a registration number, which is the position 6.8.2 sets out.

A correction arriving after publication does not edit in place. A Super Admin withdraws the set with a reason, the head teacher returns it to Draft, the mark is corrected, computation is rerun, the set is resubmitted, approved and republished, revision_number becomes 2, and every parent who downloads it sees the revision notice from 6.7.9.

## 7.4 Pin generation to parent download

1. The head teacher tells the bursar that Primary 3A results are published.
2. The bursar opens Pins and chooses Generate. There is no arm to pick, because pins are unbound. He selects the session 2026/2027 and types a purpose note reading Primary 3 parents, First Term.
3. Batch name defaults to 2026/2027 First Term batch 1. Length 10 defaults from settings. Maximum uses defaults to 3 and he leaves it, because 3 covers a parent with two children and keeps a lost slip cheap. He types a pin count of 60 for a year group of 54, which leaves spares.
4. Generate. Sixty pins are created, none of them tied to a pupil, each hashed, each with a keyed lookup hash and a ciphertext written and a purge date thirty days out. The batch is in state generated.
5. The print view opens. Sixty slips, four to an A4 page, fifteen pages, plus the distribution list. Each slip carries the school short name and logo, the pin in two groups of five, the words This pin can be used 3 times, the session, the portal address and the note that the parent will need the pupil's registration number. No slip names a pupil. The batch moves to printed.
6. The bursar cuts the slips and hands them out over two days as parents collect them, writing each pupil's name and the slip's four-character prefix on the distribution list and asking the parent to sign. He marks the batch active. Six slips are left in the drawer as spares.
7. A parent opens the portal on her phone. She types the registration number and the pin. She types a letter O where the pin has a zero, except the pin has no zero, so what she has actually mistyped is a Q for an O. The portal returns the We could not find that result copy, which mentions the character confusion. She looks again and corrects it.
8. Validation succeeds. One use is recorded. A viewing session opens for thirty minutes.
9. The term selector shows First Term available, Second Term not yet released, Third Term not yet released, Annual Cumulative available after Third Term.
10. She opens First Term. The single-column phone layout renders in under two seconds. She reads the average, the position in class, and the position in Primary 3.
11. She presses Download PDF. The A4 sheet is generated in two seconds, 140 KB, and saved to her phone. No further use is recorded.
12. She goes back and opens First Term again to re-read the head teacher's remark. Still one use.
13. Forty minutes later she reopens the page to show her husband. The viewing session has expired, so she re-enters her details. Use count becomes 2 of 3.
14. In April she checks Second Term. Use count becomes 3 of 3 and the pin is now exhausted. She collects a fresh slip from the office at the next parents meeting, which is the turnover the low default is there to force.
15. Six weeks later the child's secondary school asks for the First Term sheet. The admissions officer scans the QR code in the footer and reaches the verification page, which confirms the school, the initials, the registration number, the class, the term, the average, the grade and the position, and nothing else.

## 7.5 End-of-session promotion across arms

July 2027. Primary 3 ran two arms, 3A with 28 pupils and 3B with 26. Primary 4 will run two arms next session.

1. Third Term results are published for all twelve arms. The head teacher runs annual computation per arm, which writes an `annual_result` row for every pupil with three published terms.
2. The School Administrator closes Third Term. The close is blocked on the first attempt because Nursery 2A has a result set in Draft with marks entered. The school decides not to score that arm, voids the four stray marks, and the close succeeds.
3. The School Administrator creates session 2027/2028 with its three terms and dates.
4. Bulk arm creation is run for 2027/2028. It defaults to last session's shape: three arms of Primary 1, two of Primary 2, and so on. The administrator adjusts Primary 1 down to two arms because the intake is smaller, and Primary 4 up to two because 3A and 3B are both coming through. Twelve arms are created.
5. Promotion is opened for 2026/2027. The precondition check passes: Third Term closed, arms exist in the new session for every receiving level, annual results computed.
6. The review screen lists all 412 pupils grouped by current arm. For Primary 3A: 25 proposed Promoted, 2 proposed Repeat on an annual average below 40, and 1 flagged with no annual result because she joined in Third Term.
7. Target arms are pre-filled by balanced round-robin across Primary 4A and Primary 4B by descending annual average, so each arm receives a comparable spread from both 3A and 3B. The administrator leaves the distribution alone.
8. One of the two Repeat proposals is changed by the head teacher, who holds `promotion.decide`, to Promoted on trial with the reason Attendance was the problem, not ability. Mother has agreed to a plan.
9. The pupil with no annual result is given Promoted manually after the head teacher checks her one term.
10. Primary 6A's 22 pupils are proposed Graduated, since Primary 6 is the terminal level. No target arm is required for them.
11. Commit. In one transaction the system writes the promotion batch, closes 412 enrolments at 23/07/2027, opens 390 new enrolments from 13/09/2027, and moves 22 pupils to status graduated.
12. The 2026/2027 session moves to closed as First Term 2027/2028 is opened, and every arm in it moves to status closed and becomes read-only.
13. Class Teacher assignments do not carry over. The administrator runs Copy assignments to new session, reviews the list, changes four form teachers who have moved rooms, and commits.
14. A week into the new session the administrator notices that two pupils were sent to Primary 4B who should have gone to 4A. Reversal is not needed and would be refused anyway once marks exist. She moves the two pupils individually with a transfer, which is the correct tool.


---
