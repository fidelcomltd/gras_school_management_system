# Module 6.8: Access Pin Management

## 6.8 Access Pin Management

### 6.8.1 Purpose

A pin is the only credential in the parent-facing half of this product. It is printed on a slip, handed across a counter, and typed into a phone by somebody who may be doing it once a term. Everything about it has to survive that.

### 6.8.2 Binding: pins are not tied to a pupil

The school has settled this question and the answer is unbound. **A pin is generated against a batch and not against a pupil. Any valid pin, presented with any registration number, opens that pupil's published results, and each successful lookup counts against the pin's allowance.** A batch generated with a maximum of 3 uses gives every pin in that batch 3 uses, and the fourth attempt with that pin is refused whichever pupil it names.

This is a deliberate operational choice and it buys real things. Pins can be printed before the roster is settled, a slip can be handed to whoever turns up for a child, a parent with three children in the school needs one slip rather than three, and a lost slip is replaced from stock rather than regenerated. None of that is available under binding.

It also carries a cost that has to be written down rather than discovered later. Registration numbers at this school are sequential, printed on uniform tags and read aloud at assembly, so somebody holding one valid pin can read any pupil's results by counting upward from a number they already know, up to the number of uses on that pin. The pin allowance is therefore not just a fair-use limit. It is the main thing standing between one leaked slip and the academic records of the whole school, and the controls in 6.9.3 exist because of it.

| Consequence of leaving pins unbound | How it is handled |
| --- | --- |
| One pin can open several different pupils | Permitted by design, and each pupil opened costs a use. The per-pin control in 6.9.3 caps how many distinct pupils one pin may open before it is flagged and suspended, so ordinary sibling use passes and register-walking does not. |
| A found slip is usable against any child | The maximum uses per batch is the limit on the damage, which is why the school should set it low. A batch of 3 uses turns a found slip into at most three disclosures. A batch of 50 turns it into a directory. |
| Slips carry no pupil name, so misdelivery is invisible | Correct. The slip identifies the batch and nothing else, so the office cannot see that the wrong parent took the wrong slip, because there is no wrong slip. The register of who received which pin is the school's paper record if it wants one, and 6.8.9 prints a signature column on the distribution list for exactly that. |
| A pin cannot be revoked for one child only | Revocation is per pin or per batch. There is no narrower unit, because there is no pupil on the pin. |
| Two guardians both want access | Hand them two pins from the batch, or one slip between them. No feature needed. |

My recommendation was pre-binding, and the reasoning is recorded in Appendix A entry 35 along with the decision to override it. The recommendation is noted and the school's choice is what this document specifies. What the school must do in exchange is set a low maximum-uses value, generate per term rather than per year so that circulating stock turns over, and sign the residual-risk statement in Appendix B question 7 knowing what it now covers.

### 6.8.3 Actors and required privileges

| Operation | Privilege |
| --- | --- |
| List batches and open one | `pin.view` |
| Generate a batch | `pin.generate` |
| Render the print run, which reveals pin plaintext | `pin.print` |
| Revoke a pin or a batch | `pin.revoke` |
| Read the usage report | `pin.usage.view` |
| Change pin defaults | `settings.pin.update` |

### 6.8.4 Entity: pin_batch

The batch is the unit of everything: generation, maximum uses, printing, revocation and reporting. A pin on its own has almost no properties.

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| id | UUID | Yes | System. |
| name | String 80 | Yes | Free text, defaulted to the session and term plus a sequence, for example 2026/2027 First Term batch 1. Unique within a session. |
| session_id | UUID | Yes | The session the pins are valid for. Pins stop working when it closes. |
| purpose_note | String 200 | No | Free text recording what the batch was made for, for example Primary 3 and Primary 4 parents, First Term. Informational only. It does not restrict which pupils the pins can open, and the field label says so. |
| pin_length | Integer | Yes | 6 to 16. Defaults from settings, 10. Fixed for the batch once generated. |
| max_uses | Integer | Yes | 1 to 100. Defaults from settings, 3. Applies identically to every pin in the batch and is fixed at generation. The default is low on purpose, per 6.8.2. |
| pin_count | Integer | Yes | How many pins to generate. 1 to 2000, typed by the bursar. There is no roster to derive it from. |
| state | Enum | Yes | generated, printed, active, exhausted, revoked. |
| plaintext_purge_at | Timestamp | Yes | Generation time plus 30 days. The nightly job purges pin ciphertext at this point. |
| generated_by, generated_at | UUID, Timestamp | Yes | System. |
| revoked_by, revoked_at, revoke_reason | UUID, Timestamp, String 500 | No | Reason required on revocation. |

### 6.8.5 Entity: pin

| Field | Type | Req | Validation |
| --- | --- | --- | --- |
| id | UUID | Yes | System. |
| batch_id | UUID | Yes | Every pin belongs to exactly one batch. |
| pin_hash | String | Yes | Argon2id with a per-pin salt. The only long-term record of the pin value. Indexed for lookup by a deterministic keyed hash held alongside it, per 6.8.6, because validation has to find a pin from the value alone. |
| pin_prefix | String 4 | Yes | The first four characters in plaintext, permanently. This exists so that a bursar can identify which batch and which slip a parent is holding when they read it out over the telephone, without the system ever revealing or confirming the whole value. |
| pin_ciphertext | Bytes | No | The full value encrypted with AES-256-GCM under a key held outside the database. Written at generation, needed for printing and reprinting, purged by the nightly job at `plaintext_purge_at` and set null. |
| max_uses | Integer | Yes | Copied from the batch at generation, so a later change to the settings default does not alter an issued pin. |
| use_count | Integer | Yes | Defaults 0. Incremented per 6.8.8. |
| distinct_pupil_count | Integer | Yes | Defaults 0. The number of different pupils this pin has opened. Maintained alongside `use_count` and read by the control in 6.9.3. |
| state | Enum | Yes | unused, active, exhausted, suspended, revoked. unused becomes active on first successful use. suspended is set automatically by the spread control in 6.9.3 and is the only state an administrator can reverse. |
| revoked_at, revoked_by, revoke_reason | Timestamp, UUID, String | No | Reason required. |

There is no `pupil_id` on a pin and no foreign key from a pin to a pupil. The link between a pin and the pupils it has opened lives entirely in `pin_use`, which holds one row per viewing session: pin id, **the pupil id viewed**, opened_at, closed_at, truncated source address, truncated user agent, and the terms viewed and PDFs downloaded within that session as a small JSON array. Recording the pupil on the use row rather than on the pin is what makes the usage report and the spread control possible.

### 6.8.6 Character set, length, uniqueness and lookup

The generated character set is fixed at 31 characters and excludes everything a parent could misread from a printed slip: **ABCDEFGHJKMNPQRSTUVWXYZ23456789**. Removed are I, L and O because they are confused with 1 and 0, and removed are 0 and 1 for the same reason. Lower case is not used, and validation on entry uppercases whatever the parent types, so a phone keyboard that autocapitalises or does not is equally fine. Spaces and hyphens typed by the parent are stripped before comparison.

The default length of 10 over a 31-character alphabet gives about 8 times 10 to the 14 possibilities. Under unbound pins this number does more work than it did under binding, because a guessed pin is useful against every pupil rather than one, so the length is not reduced below 10 for any batch and the settings screen refuses to. Pins print grouped in fives with a hyphen, `H7K2M-QRW4T`, because a parent copying ten unbroken characters loses their place.

**Uniqueness is now global.** Under binding a collision between two pupils' pins was harmless. Here it is not: two identical pins in circulation would make revocation and the usage trail ambiguous. Every generated candidate is therefore checked against every pin in every non-revoked batch of every open session, and a collision is retried up to five times before the whole batch generation fails with **Pin generation failed. Try again.** In practice this branch will never execute.

**Lookup.** Validation receives a pin value and no pupil hint, so the system cannot iterate Argon2id hashes to find a match. Each pin therefore stores a second value alongside the Argon2id hash: a deterministic HMAC-SHA-256 of the normalised pin under a key held outside the database, indexed and unique. Validation computes the HMAC, finds at most one candidate row, and then verifies the Argon2id hash before accepting. The HMAC makes lookup a single indexed read, and the Argon2id verification is what stands up if the database is taken without the key.

Storage: the Argon2id hash is the record of truth and the ciphertext is a temporary convenience for printing. The reason the ciphertext exists is that the school will need to reprint a sheet of slips that were damaged, three days after generation, and a system that cannot do that will be worked around by a bursar who writes pins in a notebook. Thirty days of retained ciphertext under a key outside the database, purged automatically, is a better risk than a notebook. After purge the pin value exists nowhere in the system and lost stock is replaced by a fresh batch, not recovered. The batch screen says so, with the date: **Pin values can be reprinted until 14/01/2027. After that, generate a new batch to replace any lost slips.**

### 6.8.7 Pin scope

A pin is valid for one session, across all three terms and the annual cumulative result, until its uses run out or it is revoked or suspended.

Indefinite pins were rejected because a pin that works forever is a credential the school can never retire. One session matches the school's rhythm. With unbound pins there is a stronger argument for generating a fresh batch each term instead, and the school should: a batch that is only valid while its session is open still leaves nine months of circulating stock, and turning the stock over every term shortens that to thirteen weeks. The system does not force this, because the session is the technical boundary, but 6.8.9 defaults the batch name to the session and term to make per-term generation the obvious habit.

A pin whose session has ended stops working, and the portal says so in plain language rather than reporting an invalid pin. Old results stay reachable with a current pin: a pin issued in 2027/2028 opens the 2026/2027 results of any pupil whose number is entered, which matters when a parent needs last year's sheet for a secondary school application.

### 6.8.8 What counts as a use

This will generate more parent complaints than anything else in the product, so the rule is generous within a sitting and stated on the pin slip itself.

> **One use is counted when a registration number and pin are validated successfully and a viewing session is opened. That viewing session lasts thirty minutes and is bound to the one pupil whose registration number was entered. Within it the parent may look at every published term for that pupil, switch between them freely, and download every PDF, and no further use is counted. Entering the pin again after the viewing session has expired counts a second use, and entering the pin against a different pupil counts a use whether or not the first session is still open.**

| Event | Counts as a use? |
| --- | --- |
| Successful validation opening a new viewing session | Yes. One. |
| Viewing First Term, then Second Term, then Third Term for that pupil in the same viewing session | No. Still one use. |
| Downloading the PDF, once or five times, in the same viewing session | No. |
| Reloading the page inside the viewing session | No. The session is server-side and survives a reload. |
| Entering the same pin against a second child's registration number | Yes. A second use, and it increments the pin's distinct pupil count. This is how a parent with two children in the school spends two of their three uses. |
| A failed attempt: unknown number, wrong pin, or both | No. Failures never consume a use. They are rate limited instead. |
| Re-entering the pin for the same pupil thirty-five minutes later | Yes. Another use. |
| An attempt against a revoked, suspended or exhausted pin | No. |

The default of 3 uses per batch is deliberately tight, because under unbound pins the allowance is a security control and not only a fair-use limit. A parent with one child who opens all three terms in one sitting spends one use and has two spare. A parent with two children spends two. A school that finds this too tight should raise it to 5 rather than to 20, and the settings screen shows the consequence in plain words beside the field: **Each pin can open this many pupils' results. A slip that is lost can open this many children.**

The pin slip prints: **This pin can be used 3 times. Looking at all three terms for one pupil in one sitting counts as one use.**

### 6.8.9 Generation flow

1. The bursar opens Pins and chooses Generate. There is no arm picker and no roster, because the pins are not tied to anybody.
2. The form takes the session, a batch name defaulted to the session and term, an optional purpose note, the number of pins, the pin length and the maximum uses. Length and maximum uses default from settings. The screen states beside the maximum uses field what raising it means, per 6.8.8.
3. The bursar types the number of pins. The screen shows the school's current active pupil count as a reference point, labelled as a guide rather than a requirement, since one slip may serve two siblings and the school will want spares.
4. Generate creates the batch in state generated and the requested number of pins, each hashed, each with its ciphertext written, each unique against every pin in circulation.
5. The screen moves straight to the print view. Printing is the point of generation, and separating them by a navigation step means batches sit ungenerated.
6. The print view renders one slip per pin, four to an A4 page, with cut lines. Each slip carries the school short name and logo, the pin grouped in fives, the number of uses allowed, the session it is valid for, the portal address, the instruction that the parent will need the pupil's registration number, and the two sentences of instruction in 6.8.8. It carries no pupil name and no registration number, because there is no pupil on the pin. Rendering the print view moves the batch to state printed and writes an audit event naming the actor.
7. The print view also offers a distribution list: a numbered sheet showing each slip's four-character prefix with blank columns for pupil name, class, guardian name and signature. The school fills it in by hand as slips are handed over. This is the only record that connects a pin to a family, it lives on paper, and it exists because without it the school cannot answer a question about who was given what.
8. After distribution the bursar marks the batch active, which is a single button and exists so that the batch list distinguishes printed but not yet handed out from in circulation.

There is no replacement-pin action and no per-pupil generation, because neither has meaning without binding. A parent who has lost a slip is handed another pin from stock, and if stock has run out the bursar generates a small top-up batch.

### 6.8.10 Batch states

| State | Meaning and transitions |
| --- | --- |
| generated | Pins exist, plaintext exists, nothing has been printed. Moves to printed when the print view is rendered. A batch can be revoked from here, which is the cheapest way to unwind a mistaken generation. |
| printed | The print view has been rendered at least once. Reprinting is available until `plaintext_purge_at`. Moves to active when the bursar marks it distributed. |
| active | In circulation. Parents are using it. |
| exhausted | Set automatically by a nightly job when every pin in the batch is exhausted, suspended or revoked, or when the batch's session has closed. A cosmetic state that keeps the list readable, and it does not change any pin's behaviour. |
| revoked | Set by `pin.revoke` with a reason. Every pin in the batch is revoked in the same transaction. Terminal, and the action the school takes the moment it believes a sheet of slips has gone astray. |

Bulk revoke operates on a batch and is one button with a typed reason, because under unbound pins a suspected leak is answered by killing the whole batch and reissuing rather than by hunting one slip. Per-pin revoke operates from the batch detail view. Revocation is immediate: a parent mid-viewing-session on a revoked pin has their session invalidated on the next request and sees the copy in 6.9.4.

### 6.8.11 Batch list view and the usage report

Batch list columns: batch name, session, purpose note, pins generated, pins used at least once, pins exhausted, pins suspended, pins revoked, maximum uses, state, generated by, generated on. Filters: session, state, maximum uses. Default sort newest first. The used and exhausted counts are what tell the bursar whether distribution actually happened: a batch of a hundred printed in December showing 4 used by February means the slips are in a drawer.

The usage report is the screen the bursar opens when a parent telephones to say the pin does not work, and it is also the screen that catches misuse. It is reached by searching a pin prefix, a batch, or a registration number.

| Report element | Content |
| --- | --- |
| Pin identification | The pin prefix, its batch, the batch purpose note, the date generated, the date printed. No pupil, because a pin has none. |
| Pin position | State, uses consumed of uses allowed, distinct pupils opened, date of first use, date of most recent use. |
| Use history | One row per viewing session: date and time in WAT, **the pupil opened**, truncated source address, terms viewed, PDFs downloaded. This is the column that did not exist under binding and it is the most important one on the screen, because it is how the school sees that one pin opened eleven different children in an afternoon. |
| Spread indicator | Distinct pupils opened, shown against the flag threshold from 6.9.3, with the pin's state. A suspended pin says why and by which rule. |
| Failed attempt history | One row per failed attempt against this pin, and separately, when searched by registration number, one row per failed attempt against that number in the last thirty days, with the reason: pin not recognised, pin exhausted, pin suspended, pin revoked, result not published. This is the row that answers the actual question nine times in ten, because the parent is typing an O for a zero. |
| Actions | Revoke this pin. Revoke the batch. Reinstate a suspended pin, with a typed reason. All inline, all requiring the relevant privilege. |

Searching by registration number answers the other half of the question the school will ask, which is who has been looking at this child. It returns every viewing session opened against that pupil, with the pin prefix, the batch and the time. Under binding this list could only ever contain that child's own pins. It can now contain any pin in the school, which is precisely why the screen exists.

The report never displays a pin value, including to a Super Admin, including within the reprint window. Reprinting produces a print artefact and an audit event, and does not put the value on a screen that could be photographed over a shoulder.

### 6.8.12 Error and edge cases

| Case | Behaviour |
| --- | --- |
| Batch generated with a pin count of zero or blank | Rejected: Enter how many pins to generate. |
| Batch generated with maximum uses above 10 | Allowed, with a typed confirmation: You are about to generate pins that can each open 20 pupils' results. Type the number to confirm. The friction is deliberate and is the only place in the product that asks somebody to type a number back. |
| Batch generated before any result is published | Allowed. Slips are printed ahead of release, and a parent who looks early sees the not yet published message in 6.9.4 without spending a use. |
| Pupil transferred to another arm | Nothing happens to any pin. Pins have no arm and no pupil. |
| Pupil withdrawn | No pin is revoked, because no pin belongs to that pupil. Instead the pupil's published results become unreachable through the portal while the status is withdrawn, and the portal returns the generic not-found copy. The school decides whether to make them reachable again. |
| Pupil graduated | Results stay reachable. The parent needs the Third Term and annual result after the child has left. |
| Pin defaults changed from 3 uses to 5 | Existing pins keep 3. Only batches generated after the change get 5. |
| Ciphertext purge runs while a batch is still in state printed | Purge proceeds on schedule. The batch screen replaces the Reprint action with a line reading Pin values are no longer stored. Generate a new batch to replace any lost slips. |
| Parent reads out a pin over the telephone to the bursar | The bursar can match the first four characters against `pin_prefix` to confirm which batch it came from and see its state and failure history, but cannot see or confirm the remaining characters. |
| A pin is used against eleven pupils in one afternoon | The spread control in 6.9.3 suspends it at the threshold, the bursar sees it on the batch screen the same day, and the school decides whether to revoke the batch. This is the scenario the control exists for. |
| A parent legitimately has four children in the school and a 3-use pin | The third lookup exhausts the pin and the fourth is refused with the used-up copy. The office hands over a second slip. The school can also generate a small batch with a higher maximum for large families, which is a supported use of the purpose note field. |
| Revoked or suspended pin used | Counts nothing, returns the relevant copy in 6.9.4, and is recorded in the failed attempt history so the bursar can see the parent is holding a dead slip. |

### 6.8.13 Endpoints

| Endpoint | Notes |
| --- | --- |
| GET /pin-batches | Filtered list with counts. |
| POST /pin-batches | Generate. Body carries session, name, purpose note, pin count, length and maximum uses. Idempotency key required, per 9.8.2, so a retry cannot double-generate a batch. |
| GET /pin-batches/{id} | Detail with per-pin state, use count and distinct pupil count, never with pin values. |
| GET /pin-batches/{id}/print | Returns the print-ready PDF of slips. Requires `pin.print`. Fails with 410 after `plaintext_purge_at`. Writes an audit event. |
| GET /pin-batches/{id}/distribution-list | The numbered hand-over sheet described in 6.8.9. |
| POST /pin-batches/{id}/mark-distributed | generated or printed to active. |
| POST /pin-batches/{id}/revoke | Reason required. Revokes every pin in the batch. |
| POST /pins/{id}/revoke | Reason required. |
| POST /pins/{id}/reinstate | Clears a suspension set by the spread control. Reason required. Requires `pin.revoke`. |
| GET /pins/usage?prefix= or ?batch_id= or ?registration_number= | The usage report, in its pin-centred or pupil-centred form. |


---
