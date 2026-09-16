# 8. Result Computation Rules

The rules in 6.7.6 are restated here as an ordered algorithm, and then worked through with real numbers. The worked example is written to be used as a regression fixture: the figures in 8.4 should be loaded as test data and the assertions taken directly from the tables.

## 8.1 Inputs

| Input | Source |
| --- | --- |
| Assessment components and their maximums | Settings, live for an unpublished set, snapshot for a published one. |
| Grading bands | Same. |
| Result rules: tie-break, position scope, level position, pass mark, minimum subjects | Same. |
| Subjects in effect for the arm this term | Level mappings for the term, plus the arm's include exceptions, minus its exclude exceptions. |
| Pupils to compute over | Pupils with an open enrolment in the arm, status active, as at the moment computation runs. |
| Marks | `subject_score` rows for those pupils and subjects in that term, excluding voided rows. |
| Times school opened | The term record. |

## 8.2 Computation order

1. Resolve the pupil set and the subject set. If either is empty, stop with the message from 6.7.12.
2. For each pupil and each subject, sum the non-examination component marks into ca_total.
3. Add the examination mark to produce subject_total, or carry ca_total alone where exam_absent is true.
4. Resolve grade and remark for subject_total against the grading bands. A total matching no band stops the whole computation.
5. For each subject, over the counted pupils only, which excludes examination absentees, compute highest, lowest and class average to one decimal place.
6. For each subject, rank all pupils including absentees on subject_total descending, applying the tie-break rule, to produce subject_position.
7. For each pupil, sum subject_total across subjects into total_obtained, and multiply the subject count by 100 into total_obtainable.
8. Divide to produce average to two decimal places, and resolve the overall grade against the rounded average.
9. Rank pupils on total_obtained descending within the arm, applying the tie-break rule, to produce arm_position. Write arm_pupil_count.
10. Where a level position is required, rank every pupil in every arm of the level on average descending to produce level_position. Write level_pupil_count.
11. Delete every existing computed row for the result set and write the new ones in one transaction. Clear needs_recompute.

Steps 5 and 6 are separate passes over the same data with different populations. Collapsing them into one loop is the mistake that produces a class average including an absentee, so they are specified apart.

## 8.3 Formulas

| Quantity | Formula |
| --- | --- |
| ca_total | Sum of marks for every component where is_examination is false. |
| subject_total | ca_total plus exam_mark, or ca_total when exam_absent. |
| class_average for a subject | Sum of subject_total over counted pupils, divided by the count of counted pupils, rounded half up to one decimal place. |
| total_obtainable | Number of subjects in effect multiplied by 100. |
| total_obtained | Sum of subject_total across the pupil's subjects. |
| average | total_obtained divided by the number of subjects, rounded half up to two decimal places. |
| arm_position | Competition rank on total_obtained descending. Equal totals share a rank and the following rank skips by the number tied. |
| level_position | Competition rank on average descending across all arms of the level. |
| cumulative_average, simple | Mean of the term averages actually sat, rounded half up to two decimal places. |
| cumulative_average, weighted | Sum over terms sat of term average multiplied by that term's weight, divided by the sum of those weights, rounded half up to two decimal places. |

## 8.4 Worked example

### 8.4.1 Setup

Session 2026/2027, First Term. Class level Primary 3, running two arms: Primary 3A with 28 active pupils and Primary 3B with 26, giving 54 pupils at the level. Assessment structure is the seeded one per 6.2.13: 1st CA 20, 2nd CA 20, Examination 60, with no assignment component. Grading scale is the seeded nine bands per 6.2.13. Result rules are the defaults: arm-scoped primary position, level position shown, shared-position tie-breaking, pass mark 40.

Pupil under examination: Adaeze Okafor, `GRAS/2024/0087`, female, born 04/03/2018, so 8 years old as at the term end date of 18/12/2026, enrolled in Primary 3A.

Primary 3 actually takes nine subjects. Four are shown here so the arithmetic can be followed on one page. Since total_obtainable is the subject count multiplied by 100, nothing about the method changes with nine.

### 8.4.2 Adaeze's marks and subject totals

| Subject | CA1 /20 | CA2 /20 | CA total | Exam /60 | Total /100 | Grade |
| --- | --- | --- | --- | --- | --- | --- |
| English Studies | 18 | 16 | 34 | 52 | 86 | A |
| Mathematics | 17 | 16 | 33 | 45 | 78 | B |
| Basic Science and Technology | 14 | 12 | 26 | 38 | 64 | C+ |
| Cultural and Creative Arts | 19 | 18 | 37 | 55 | 92 | A+ |

*The CA total column deliberately carries no maximum in its heading. It is the sum of every component where `is_examination` is false, which is 40 under this structure and a different number under another. Per 6.2.13 the value 40 must not appear in a column heading, a validation message, a fixture or anywhere in code.*

*Grades resolved against the seeded nine bands: 86 falls in A (85 to 89), 78 in B (75 to 84), 64 in C+ (60 to 69), 92 in A+ (90 to 100).*

Remarks follow the band: Excellent, Very good, Average, Very excellent.

total_obtained is 86 plus 78 plus 64 plus 92, which is **320**. total_obtainable is 4 multiplied by 100, which is **400**. average is 320 divided by 4, which is **80.00**, and 80.00 falls in band B (75 to 84), so the overall grade is **B, Very good**.

### 8.4.3 A pupil absent for one examination

Musa Ibrahim, `GRAS/2026/0104`, also in Primary 3A, was absent for the Mathematics examination. His Mathematics row is CA1 13, CA2 11, giving a continuous assessment total of 24, with exam_absent true.

- His subject_total for Mathematics is 24, the continuous assessment total alone.
- 24 falls in band E (20 to 39), so his grade is E and his remark is Not Now.
- The result sheet prints ABS in his examination column and 24 with an asterisk in his total column, keyed to the footnote ABS: absent for the examination.
- He is ranked in Mathematics. With 24 he is last, 28th of 28.
- He is excluded from Mathematics' class average, highest and lowest. The counted population for Mathematics is therefore 27, not 28, while the ranked population is 28.

### 8.4.4 Class statistics for Primary 3A

| Subject | Counted | Sum of totals | Class average | Highest | Lowest |
| --- | --- | --- | --- | --- | --- |
| English Studies | 28 | 1915 | 68.4 | 91 | 44 |
| Mathematics | 27 | 1612 | 59.7 | 88 | 31 |
| Basic Science and Technology | 28 | 1683 | 60.1 | 82 | 39 |
| Cultural and Creative Arts | 28 | 2010 | 71.8 | 92 | 51 |

*1915 divided by 28 is 68.392857, which rounds half up to 68.4. 1612 divided by 27 is 59.703704, which rounds to 59.7. Mathematics is the only subject with a counted population of 27, because of Musa's absence, and its lowest of 31 excludes his 24.*

### 8.4.5 Subject positions

| Subject | Total | Position | Working |
| --- | --- | --- | --- |
| English Studies | 86 | 3rd of 28 | Two pupils scored above: 91 and 88. |
| Mathematics | 78 | 6th (tied) of 28 | Five pupils scored above: 88, 85, 84, 81, 80. Chidi Nwosu also scored 78. Both are 6th. The next pupil, on 74, is 8th. |
| Basic Science and Technology | 64 | 12th of 28 | Eleven pupils scored above. |
| Cultural and Creative Arts | 92 | 1st of 28 | 92 is the highest score in the arm, which is why the highest column for this subject reads 92. |

The Mathematics tie is the case to test. Under the default shared_position rule both pupils on 78 print 6th (tied) and the next position is 8th, not 7th. Under exam_then_ca the tie would be broken on the higher examination mark: Adaeze scored 45 in the Mathematics examination and Chidi scored 41, so Adaeze would be 6th and Chidi 7th, with the next pupil 8th either way.

### 8.4.6 Overall position within the arm

| Pupil | Total obtained | Average | Position in Primary 3A |
| --- | --- | --- | --- |
| Ngozi Eze | 337 | 84.25 | 1st of 28 |
| Chidi Nwosu | 329 | 82.25 | 2nd of 28 |
| Adaeze Okafor | 320 | 80.00 | 3rd of 28 |

*Ranked on total obtained descending. Every pupil in Primary 3A takes the same four subjects in this example, so totals are directly comparable and no normalisation is applied. Ngozi holds the highest score in English Studies, Mathematics and Basic Science and Technology, which is consistent with the highest column in 8.4.4.*

### 8.4.7 Level position across the two arms

`show_level_position` is true, so a second position is computed across all 54 pupils of Primary 3, ranked on average rather than on total, because two arms of the same level may take different subject sets through per-arm exceptions and totals would then be incomparable.

| Pupil | Arm | Average | Position in arm | Position in Primary 3 |
| --- | --- | --- | --- | --- |
| Ngozi Eze | Primary 3A | 84.25 | 1st of 28 | 1st of 54 |
| Emeka Adigwe | Primary 3B | 83.25 | 1st of 26 | 2nd of 54 |
| Chidi Nwosu | Primary 3A | 82.25 | 2nd of 28 | 3rd of 54 |
| Funmi Ajayi | Primary 3B | 80.50 | 2nd of 26 | 4th of 54 |
| Adaeze Okafor | Primary 3A | 80.00 | 3rd of 28 | 5th of 54 |

This table is the fairness argument in one place. Emeka is first in his arm and second in the level. Adaeze is third in her arm and fifth in the level. A parent shown only an arm position would conclude that Emeka outperformed Chidi, which he did on average, and that Adaeze was third of her year group, which she was not. Printing both lines, each naming its cohort and its size, is the only way to answer the comparison parents will make anyway.

### 8.4.8 What Adaeze's result sheet prints

| Field | Value |
| --- | --- |
| Class | Primary 3A |
| Session and term | 2026/2027, First Term |
| Number of pupils in class | 28 |
| Total marks obtainable | 400 |
| Total marks obtained | 320 |
| Average | 80.00 |
| Overall grade and remark | B, Very good |
| Position in Class (Primary 3A, 28 pupils) | 3rd |
| Position in Primary 3 (all 2 arms, 54 pupils) | 5th |
| Times school opened | 58 |
| Times present, times absent | 56, 2 |
| Next term begins | 11/01/2027 |

### 8.4.9 Annual cumulative and promotion

Adaeze's three term averages for 2026/2027 are First Term 80.00, Second Term 78.25 and Third Term 82.50.

- Under the default simple average: 80.00 plus 78.25 plus 82.50 is 240.75. Divided by 3 that is 80.25. Cumulative grade **B, Very good**.
- Under the weighted alternative with weights 20, 30 and 50: 80.00 times 0.20 is 16.000, plus 78.25 times 0.30 is 23.475, plus 82.50 times 0.50 is 41.250, giving 80.725, which rounds to **80.73**. Same grade, different number, and the difference is why the method is a setting and why changing it after Third Term is published is locked.
- Annual position: ranked on cumulative average within Primary 3A.
- Promotion: cumulative average 80.25 is at or above the promotion threshold of 40. `require_core_pass` is true and the core subjects are English Studies and Mathematics. Her annual subject means are 85.33 in English Studies, from 86, 82 and 88, and 76.33 in Mathematics, from 78, 71 and 80. Both are at or above the pass mark of 40. Proposed outcome: **Promoted to Primary 4**.

## 8.5 Using this as a fixture

Load Primary 3A and Primary 3B with the pupils and marks above, run computation, and assert: Adaeze's four subject totals, four grades (A, B, C+, A+) and four subject positions; the Mathematics tie displaying as 6th (tied) with the next position 8th; the Mathematics counted population of 27 against a ranked population of 28; Mathematics lowest of 31 rather than 24; the four class averages to one decimal place; total obtained 320, average 80.00, overall grade B, arm position 3rd of 28, level position 5th of 54; Musa's Mathematics total of 24 with ABS printed and grade E; and the annual cumulative of 80.25 under simple average and 80.73 under 20, 30, 50 weighting. Any change to the rounding rule, the tie-break default or the absentee treatment will break at least one of those assertions, which is the point.

Two coverage gaps this fixture does not close, and which need their own unit tests rather than a second end-to-end fixture: no total in it lands in **D (40 to 49)** or in **F (0 to 19)**, so band resolution at the bottom of the scale is unexercised here. F matters particularly because it is the band this specification added rather than the school, and it is the one a contiguity regression would expose first.


---
