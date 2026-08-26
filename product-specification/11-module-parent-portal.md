# Module 6.9: Parent Result-Checking Portal

## 6.9 Parent Result-Checking Portal

### 6.9.1 Purpose and assumptions

One public page, no accounts, no passwords, no app. The parent using it is on a low-end Android phone on a 3G connection, standing outside the school gate, possibly with the pin slip in the same hand as the phone. Three quarters of traffic will arrive in the seventy-two hours after each release and then almost none until the next term.

Nothing in this module authenticates a person, and since 6.8.2 it does not authenticate a relationship to a child either. It authenticates a slip of paper and nothing more. That is the security position and the rest of the section is written with it in mind.

### 6.9.2 Flow

1. The parent lands on the portal. One screen: school logo, school name, two fields, one button. Registration number and pin. Nothing else above the fold, no news, no announcements.
2. The registration number field accepts the number with or without separators and uppercases as typed. The pin field is a plain text input, not masked, because a masked field on a phone with a printed pin produces typing errors and there is nothing secret about the pin from the person holding the slip.
3. Submit validates the pin, then resolves the registration number. On success a viewing session opens, bound to that one pupil, and lasts thirty minutes. One use is counted.
4. The parent lands on a term selector listing First Term, Second Term, Third Term and Annual Cumulative in that order, each labelled with its state: Available, Not yet released, or for annual, Available after Third Term results are released. Unavailable entries are visible and disabled, not hidden, because a parent who cannot see Third Term at all assumes the site is broken.
5. Selecting an available term renders the result on screen, laid out for a narrow phone in a single column rather than as a shrunken A4 sheet.
6. A Download PDF button produces the A4 sheet. Downloading counts no additional use.
7. The parent may go back to the term selector and open another term for the same pupil without re-entering anything and without spending a further use.
8. Looking up a second pupil requires returning to the landing page and entering that pupil's registration number with the pin again. The screen offers this as a labelled action, Check another pupil, with the words This will use one more of your pin's uses, because a parent with two children should not have to discover that by accident.

The on-screen rendering and the PDF carry the same values from the same payload described in Appendix C. They differ only in layout.

### 6.9.3 Rate limiting, spread control, and the attacker model

The attacker to design against is not sophisticated. It is a parent who wants to know whether their neighbour's child did better, or an older pupil with a phone and their classmates' registration numbers. Registration numbers are sequential and publicly visible on uniform tags. Pins are the only secret, they are printed on paper, and under 6.8.2 any one of them opens any pupil. The controls below are what remain between a found slip and a whole cohort.

| Control | Rule |
| --- | --- |
| Maximum uses per pin | The first and strongest control, set per batch at generation and defaulting to 3. A found slip discloses at most that many pupils and then stops working on its own. Everything else on this list is a backstop to it. |
| Spread control, per pin | A pin that opens more than **3 distinct pupils** in a session, or more than **2 distinct pupils within 10 minutes**, is set to state suspended immediately. The parent sees the suspended copy in 6.9.4 and is told to contact the office. A bursar with `pin.revoke` can reinstate it with a typed reason, which is a thirty-second conversation for the family with four children and a deliberate obstacle for somebody walking the register. Where the batch maximum is 3 the use limit bites first and this control rarely fires, which is intended: it exists to protect the school on the day somebody sets the maximum to 20. |
| Per source address | 10 failed attempts in 15 minutes, then a 30 minute block on that address. The counter is per address across all registration numbers, so walking the register from one phone is stopped after ten tries. Successful lookups from one address against more than 5 distinct pupils in an hour are additionally flagged to the bursar's screen, not blocked, because a school office computer helping parents will legitimately do this. |
| Per registration number | 5 failed attempts in 60 minutes across all source addresses, then a 60 minute block on that registration number. This stops a distributed attempt against one child. |
| Global | A soft ceiling on total attempts per minute, with a queue and a wait message rather than failures, so that the release-day surge is absorbed rather than rejected. |
| Deliberate response delay | Every validation response, success or failure, is padded to a minimum of 400 milliseconds so that timing does not reveal whether a registration number exists or whether the pin or the number was the problem. |
| Uniform failure messaging | An unknown registration number and an unrecognised pin return the same message. See 6.9.4. The portal never confirms that a registration number exists, and never reveals a pupil's name before a pin has validated. |
| No enumeration surface | There is no endpoint that lists pupils, no autocomplete on the registration number field, no sibling lookup, and no next or previous navigation between pupils. Robots are excluded by a noindex header and a robots.txt disallow, so a result sheet cannot be found through a search engine. |
| Attempt logging | Every attempt, successful or not, writes a `portal_attempt` row with the registration number tried, the pin prefix, the outcome, the truncated source address and the timestamp. This feeds the failed attempt history in the bursar's usage report and is purged after 90 days. |
| No lockout of the pupil | A blocked registration number unblocks itself after 60 minutes. Nothing about a failed attempt consumes a use, because a parent locked out of their own child's result by somebody else's guessing is a worse outcome than the guessing. |

The residual risk is a person who obtains any printed slip. Under unbound pins that is no longer a risk to one child, it is a risk to as many children as the pin has uses, chosen by whoever holds it. The maximum-uses setting is therefore the school's main lever and the reason 6.8.4 defaults it to 3. The rest is detection rather than prevention: the usage report shows which pupils each pin opened and when, so the school can tell a parent exactly what was seen. This is stated in 9.9 as an accepted risk with the school's sign-off recorded in Appendix B question 7.

### 6.9.4 Every error state, with the copy parents see

The copy below is approved and is what the interface uses. It is written for a parent, not for an engineer. It never uses the words invalid, error, denied or authentication, and it never states which of the two fields was wrong.

| Situation | Heading | Body and action |
| --- | --- | --- |
| Registration number not found, or pin not recognised | We could not find that result | Check the registration number and the pin on your slip and try again. Letters can be mixed up: the pin never contains the letter O or the number 0, or the letter I or the number 1. If it still does not work, take the slip to the school office. Button: Try again. |
| Pin has been used its maximum number of times | This pin has been used up | This pin has been used 3 times, which is the number allowed. Take your slip to the school office and ask for a new one. Button: Back. |
| Pin has been suspended by the spread control | This pin needs to be checked | This pin has been used for several different pupils, so we have paused it. Please take it to the school office and they will sort it out for you. Button: Back. |
| Pin has been revoked | This pin is no longer active | The school has cancelled this pin. Please contact the school office for a new one. Button: Back. |
| Pin belongs to a session that has ended | This pin has expired | This pin was for the 2025/2026 session. Ask the school office for a pin for the current session. Button: Back. |
| Validated, but no term has a published result | Results have not been released yet | The school has not released any results for this pupil yet. Please check again after the school tells you results are ready. Your pin has not been used up. Button: Back. |
| Validated, parent selects a term that is not published | Second Term results are not out yet | The school has not released Second Term results for this class. First Term results are available. Button: See First Term. |
| Validated, term is published but this pupil has no result in it | There is no result for this term | This pupil did not have a result recorded for Second Term. If you think this is a mistake, please contact the school office. Button: Back. |
| Pupil is withdrawn or transferred | We could not find that result | The same copy as an unknown registration number, and no use is consumed. The portal does not disclose that the pupil has left the school, because that is not information a pin holder has necessarily been told by the family. |
| A published result was withdrawn by the school | This result is being corrected | The school has taken this result down to correct it. A corrected copy will be available shortly. Please check again in a day or two. Button: Back. |
| Viewing session expired | Your session has ended | For security, we closed the page after 30 minutes. Enter the registration number and pin again to carry on. This will count as one more use. Button: Enter details. |
| Parent asks to check a second pupil | Check another pupil | You will need that pupil's registration number. This will use one more of your pin's uses. You have 2 left. Buttons: Continue, Back. |
| Too many failed attempts from this device | Too many tries | Please wait 30 minutes and try again. If your slip is not working, the school office can check it for you. No button. |
| Too many failed attempts on this registration number | Too many tries for this pupil | Please wait an hour and try again, or ask the school office to check the slip. No button. |
| Portal under heavy load | The site is busy | A lot of parents are checking results right now. Please wait a moment and press the button again. Button: Try again. |
| Server fault | Something went wrong on our side | This is not your fault and your pin has not been used. Please try again in a few minutes. Button: Try again. |

Where a use would have been consumed and something then failed, it is not consumed. A use and its `pin_use` row are written in the same transaction, only after the result payload has been assembled successfully.

### 6.9.5 Viewing session handling

- A successful validation creates a server-side viewing session with a random opaque token in an HttpOnly, Secure, SameSite=Strict cookie. The pin value is never stored in the session and never sent back to the browser.
- The session lasts thirty minutes from creation. It is not extended by activity, because a fixed window is easier to explain on a printed slip than a sliding one.
- The session grants access to exactly one pupil id, resolved at validation from the registration number that was typed. A tampered session token, or a request naming any other pupil, returns the generic not-found copy. Nothing in the session carries the pin, so a stolen cookie cannot be replayed against a different child.
- Opening a second pupil creates a second viewing session and consumes a second use. The two sessions can be open at once, which is what a parent with two children will do, and the interface offers a switch between them for the remainder of the thirty minutes.
- The session shows a quiet countdown in the last five minutes: Your page will close in 4 minutes.
- Closing the browser ends nothing server-side. The session expires on its own, and a returning parent within the window on the same device resumes without re-entering the pin and without consuming a second use.
- Revoking or suspending a pin invalidates every open viewing session opened with it within one request.

### 6.9.6 The PDF

| Requirement | Specification |
| --- | --- |
| Page size | A4 portrait, 210 by 297 mm, with 12 mm margins. A4 rather than Letter because every printer and every business centre in the country stocks A4. |
| Length | One page per term wherever the subject count allows. With nine subjects, the seven affective traits, the five psychomotor traits, attendance, both remarks and the grading key, one page is achievable at 8 point body text and it is what the school should aim for. Where the subject count exceeds fourteen the sheet flows to a second page and the header block repeats. The layout must not shrink text below 7 point to force one page. |
| Print fidelity in black and white | The sheet must be fully readable when photocopied in monochrome. No colour is load-bearing: grade bands are not colour-coded, the position is not highlighted, and every shaded cell uses a tint no darker than 15 per cent so that text stays legible. Borders carry the structure, not fills. |
| Generation approach | Server-side rendering to PDF from a template, not client-side. The server has the fonts, the layout is identical for every parent, and a low-end phone is not asked to render a table. The generated file is cached against the result set id, the pupil id and the revision number, so the second parent to download the same sheet gets a cached file. Cache is invalidated by republication, which changes the revision number. |
| File size | Under 200 KB. Fonts subset, logo and signature embedded once as pre-sized images, no colour profiles. A parent on a metered connection is downloading this on data. |
| File name | Registration number, term and session with separators safe for Android and Windows, for example GRAS-2026-0041_First-Term_2026-2027.pdf. |
| Content | Exactly the fields in Appendix C, rendered from the result set's configuration snapshot. The pin that opened the sheet appears nowhere in it, not in the metadata and not in the footer. |

#### Verification against forgery

Printed results get altered. A pupil with a 38 in Mathematics becomes a pupil with an 88 between the school gate and a secondary school admissions office, and the school is then asked to confirm a sheet it cannot recognise. The PDF therefore carries two verification marks.

1. A QR code in the footer, 20 mm square, encoding a URL of the form `portal.example/verify/{token}` where the token is a random 22-character opaque string stored on the result and not derivable from the pupil's details.
2. The same token printed beneath the QR code as human-readable text in groups of five, so a person without a QR reader can type it.

The verification page is public, needs no pin, and deliberately shows very little: the school name, the pupil's initials and registration number, the arm display name, the term and session, the total obtained, the average, the overall grade, the position, and the date the result was issued. It shows no subject-level marks, no guardian details and no full name. That is enough for an admissions officer to check a sheet against and not enough to be a second route into a child's full record. Where the result has since been withdrawn or revised, the verification page says so: **This result was revised on 19/01/2027. The sheet you are holding may be out of date.**

The token is generated at first publication and regenerated on each republication, so a token from a withdrawn revision resolves to the revision notice rather than to stale figures.

### 6.9.7 On the result template

The school has not supplied its result sheet template. It is coming.

This section therefore specifies the data contract rather than a layout. Appendix C lists every value the sheet must be able to render, grouped as header block, per-subject rows, summary block, affective block, psychomotor block, attendance block, remarks block and footer block, with the source of each value and its format. The portal payload endpoint returns exactly that structure.

When the template arrives, fitting it is a layout exercise against the contract. It is not a re-specification. No field in Appendix C should need to be added to the data model, no computation should need to change, and no endpoint should need a new shape. The two questions the template will answer are which of the contract fields the school's sheet actually shows and in what order, and both are recorded in Appendix B question 1. If the template turns out to require a value that is not in Appendix C, that is a defect in Appendix C and the contract is amended, not worked around in the renderer.

Until the template arrives, the build proceeds against a plain default layout implementing the contract in the order Appendix C lists it. That layout is throwaway and should not be polished.

### 6.9.8 Performance and the phone

- First contentful paint under 2 seconds on a 3G connection and a device comparable to a 2019 entry-level Android.
- The landing page ships under 60 KB total including the logo, with no web font, no analytics script and no framework bundle. Two fields and a button do not need one.
- The result view ships under 150 KB.
- Validation round trip under 1 second at the 95th percentile, excluding the deliberate 400 millisecond pad. The pin lookup is a single indexed read on the keyed hash from 6.8.6 followed by one Argon2id verification, so validation cost does not grow with the number of pins in circulation.
- PDF generation under 3 seconds at the 95th percentile, from cache under 500 milliseconds.
- Everything works without JavaScript except the countdown timer. The form posts, the term selector is links, the PDF is a link. A parent on a browser that fails to run a bundle still gets their child's result.
- Tap targets at least 44 by 44 pixels. Text at least 16 pixels, because a parent reading a result is often reading it without their glasses.
- The portal is a separate deployment from the back office, on its own subdomain, with read-only database access limited to the published-result and pin tables and write access limited to `pin_use` and `portal_attempt`. An attack on the portal cannot reach the pupil register.

### 6.9.9 Endpoints

| Endpoint | Notes |
| --- | --- |
| GET /portal | The landing page. No parameters, nothing cached per parent. |
| POST /portal/lookup | Registration number and pin. Rate limited and spread-checked per 6.9.3. On success sets the viewing session cookie, writes the `pin_use` row naming the pupil, and returns the term list with per-term availability. On failure returns one of the copies in 6.9.4 with a uniform 200 status and no field-level detail. |
| GET /portal/terms | The term list for the open viewing session. |
| GET /portal/result/{term_id} | The full payload in the Appendix C shape, for the pupil the session is bound to. |
| GET /portal/result/{term_id}/pdf | The A4 PDF. Cached. |
| GET /portal/annual | The annual cumulative payload. |
| POST /portal/end | Ends the viewing session early, for a parent using a shared phone. |
| GET /verify/{token} | Public verification page. No session, no pin, rate limited at 30 requests per address per hour. |


---

---

## 6.9.10 Amendment: weekly reports on the portal

The portal now serves two kinds of document per term. Full specification of the module behind it is in `20-module-weekly-reports.md`; the portal-side rules are here.

**The term selector gains a second row per term.** Where 6.9.2 step 4 listed First Term, Second Term, Third Term and Annual Cumulative, each term now offers two entries:

- `First Term result` with its existing availability states.
- `First Term weekly reports`, showing Available where at least one week is published and Not available yet otherwise.

**No additional pin use is consumed.** A parent inside a viewing session may read the result, the weekly reports, and every published week, and download every PDF, for one use. This follows 6.8.8 without exception: the use is bound to the pupil and the session, not to the document. A parent who has spent one of three uses can read thirteen weeks of notes and three terminal sheets in the same half hour.

**Selecting weekly reports lands on a list with one row per week of the term**, numbered and dated as `Week 4: 12/01/2027 to 16/01/2027`, each row showing its published state and a one-line preview from the teacher's comment on the most recently filled day. Unpublished and empty weeks are visible and disabled with the label `Not available`, following the same rule as unavailable terms: a parent who cannot see Week 7 at all assumes the site is broken.

**Opening a week** shows the five day panels in the paper form's order and wording. On a phone the panels stack as one card per day, and **empty lines are omitted from the card** rather than rendered as blank rules, because eight empty labels on a phone screen is noise. The downloadable PDF keeps every line whether filled or not, per `23-appendix-g-weekly-report-contract.md` G.3, so a parent can write on the printout.

**The Parent's Comment line is read-only on the portal.** It displays what a teacher transcribed from the paper copy. There is no parent-facing input anywhere in this module, and the portal remains a read-only surface. `25-open-conflicts-to-resolve.md` item 4 records the alternatives, one of which would add a write endpoint; that is not built and should not be built without the decisions listed there.

**Result sheet routing.** The result view and its PDF now render one of two layouts depending on the pupil's section, per Appendix E.1. The portal makes no decision here: it requests the payload and the server returns the correct variant with a `section` discriminator, so a single client renders both.

**What is still excluded from every portal surface.** Nothing from the expanded pupil record reaches the parent. No health data, no allergy, no medication, no preferred hospital, no authorised-pickup list, no barred-persons information, no admission documents, no contact occupations or addresses, and no section G free text. The portal payload carries what the result sheet and the weekly sheet print and nothing else, per 9.9 and `25-open-conflicts-to-resolve.md` item 9.
