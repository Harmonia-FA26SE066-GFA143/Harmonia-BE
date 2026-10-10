# Harmonia – Use Case Descriptions

Brief use case description table for Report 3 (SRS), in the form **ID / Use Case / Actors / Use Case Description**.

- **Numbering:** canonical codes from `use-case-list` (S-01…S-05, UC-01…UC-35, plus extension use cases UC-07E, UC-25a, UC-25b, UC-34E). Same codes as `harmonia-feature-list`.
- **Count:** 44 use cases = 39 actor-initiated + 5 reached only via «include»/«extend» (S-05, UC-07E, UC-25a, UC-25b, UC-34E).
- **System User** is an abstract actor generalized by all four concrete actors, so S-01…S-04 are modelled once rather than repeated per role.
- The liturgical program is planned **by day**, not by week; FE-16a (copy previous week) was dropped. (decided 2026-09-30)
- There is **no Instrumentalist role**. An instrument player is a Choir Member with an approved instrument skill.
- **Member records are maintained by the Admin (UC-31).** For the Choir Director, "managing members" means assigning them to songs (UC-24 → UC-27); UC-18 is view-only. (decided 2026-09-29)

## A. Account & Session — shared by every actor

| ID | Use Case | Actors | Use Case Description |
|---|---|---|---|
| S-01 | Login | All actors (System User) | The actor enters their registered account credentials. The system validates the credentials and grants access to the functions permitted for the actor's assigned role. (FE-01) |
| S-02 | Logout | All actors (System User) | The actor ends the current working session. The system terminates the session and returns to the login screen. |
| S-03 | Change / Forgot Password | All actors (System User) | The actor changes their password, or requests a reset when the password is forgotten. The system verifies the actor's identity before applying the new password. |
| S-04 | View Notifications | All actors (System User) | The actor opens the notification list to read system messages such as song list approval results, participation requests, assignment notices and practice feedback, and marks them as read. |
| S-05 | Send Notification | None — «include» from UC-12, UC-17, UC-23, UC-27 | Reusable behaviour in which the system composes and delivers a notification to the target members or leaders. It is always executed as part of a base use case and is never started directly by an actor. |

## B. Choir Member — Mobile App (FE-01…FE-14)

| ID | Use Case | Actors | Use Case Description |
|---|---|---|---|
| UC-01 | View Personal Profile | Choir Member | The member views their own profile information such as full name, contact details, choir and membership status. (FE-01) |
| UC-02 | View Assigned Role & Approved Skills | Choir Member | The member views the role assigned by the Choir Director together with the vocal parts, instrument skills and other capabilities that have been approved. (FE-02) |
| UC-03 | Declare Skill & Track Approval Status | Choir Member | The member declares a new skill (Soprano, Alto, Tenor, Bass, Guitar, Organ, Solo Singing or Psalmist) and follows its status until the Choir Director approves or rejects it. (FE-03, FE-04) |
| UC-04 | View Upcoming Events & Rehearsal Schedule | Choir Member | The member views upcoming Masses, ceremonies, performances and rehearsals together with their date, time, place and liturgical context. (FE-05) |
| UC-05 | Confirm Event Participation | Choir Member | The member responds to a participation request by confirming, declining or marking themselves as unsure. The system records the response and updates the Choir Director's participation view. (FE-06) |
| UC-06 | View Approved Song List for Event | Choir Member | The member views the song list that the Parish Priest or Liturgy Committee has approved for a specific event. (FE-07) |
| UC-07 | Access Music Materials | Choir Member | The member opens sheet music, lyrics, sample audio and rehearsal materials for the songs and skills assigned to them. (FE-08) |
| UC-07E | Search / Filter Materials | Choir Member | «extend» UC-07. When the material list is long, the member narrows it down by song title, liturgical season, vocal part or instrument. |
| UC-08 | Mark Material Learning Progress | Choir Member | The member marks each material as learned or as needing more practice, so that both the member and the Choir Director can follow the progress. (FE-09) |
| UC-09 | Receive & Complete Practice Assignment | Choir Member | The member receives a practice assignment from the Choir Director, records the practice audio inside the application and submits it before the deadline. (FE-10, FE-11) |
| UC-10 | View Practice Submission Result & Feedback | Choir Member | The member views the status of each submission (Submitted, Passed, Needs Revision or Overdue) together with the written feedback from the Choir Director. (FE-12, FE-13) |
| UC-11 | View Participation & Practice History | Choir Member | The member reviews their own past service participation and practice records across events and liturgical seasons. (FE-14) |

## C. Parish Priest / Liturgy Committee — Web System (FE-15…FE-23)

| ID | Use Case | Actors | Use Case Description |
|---|---|---|---|
| UC-12 | Manage Daily Liturgical Program | Parish Priest / Liturgy Committee | The Parish Priest opens the liturgical calendar by day. For each date the system shows the celebration name, rank and liturgical season taken from an external Catholic calendar API. The priest creates the liturgical events of that day (Masses, ceremonies, special events), each with its own time, Mass type, ceremony type, location and special requirements; the event's liturgical season is pre-filled from the API and can be changed. A single day may contain multiple events at different times. Each event is published on its own, and publishing notifies the Choir Director (includes S-05). (FE-15, FE-16) |
| UC-13 | Review & Approve Song List with Notes | Parish Priest / Liturgy Committee | The Parish Priest reviews the song list proposed by the Choir Director and approves it, rejects it or requests a revision, attaching notes on theme, liturgical season, ceremony context or pastoral concerns. (FE-17, FE-18, FE-19) |
| UC-14 | View Final Approved Song List | Parish Priest / Liturgy Committee | The Parish Priest views the finalised approved song list of an event for reference before the celebration. (FE-20) |
| UC-15 | Monitor Choir Preparation Status | Parish Priest / Liturgy Committee | The Parish Priest views how ready the choir is for an important event, including participation confirmations, roster status and practice progress. (FE-21) |
| UC-16 | View Service & Song Usage Reports | Parish Priest / Liturgy Committee | The Parish Priest views reports on choir service history, how frequently each song has been used and the preparation results of past events. (FE-22) |
| UC-17 | Send Notes / Requests to Choir Director | Parish Priest / Liturgy Committee | The Parish Priest sends general notes or requests about upcoming liturgical programs to the Choir Director. Includes S-05 to deliver the message. (FE-23) |

## D. Choir Director — Web System (FE-24…FE-46)

| ID | Use Case | Actors | Use Case Description |
|---|---|---|---|
| UC-18 | View Choir Members & Skills | Choir Director | The Choir Director views the list of choir members with their approved skills (vocal parts, instruments) and membership status, as the basis for assigning members to songs (UC-24 → UC-27). The Choir Director does not create or edit member records — that is done by the Admin (UC-31). (FE-24) |
| UC-19 | Approve Member-Declared Skills | Choir Director | The Choir Director reviews the skills declared by members and approves or rejects each one. Only approved skills are used when the system suggests a service roster. (FE-25) |
| UC-20 | Schedule Rehearsals | Choir Director | The Choir Director creates rehearsal sessions for an event, specifying time, place and the songs to be rehearsed. (FE-26) |
| UC-21 | Manage & Classify Music Library | Choir Director | The Choir Director uploads sheet music, lyrics, sample audio and rehearsal materials, then classifies each song by liturgical season, Mass type, ceremony type, theme and vocal or instrument requirements. (FE-27, FE-28, FE-29) |
| UC-22 | Manage Song List Lifecycle (Propose / Submit / Revise) | Choir Director | The Choir Director proposes a song list for an event, submits it to the Parish Priest for approval and revises it when a revision is requested. (FE-30, FE-31, FE-32) |
| UC-23 | Manage Member Participation Confirmation | Choir Director | The Choir Director sends participation confirmation requests to choir members and monitors their responses in real time. Includes S-05. (FE-33, FE-34) |
| UC-24 | Define Song Personnel Requirements | Choir Director | First step of the Choir Director's member management, which means assigning members to each song (UC-24 → UC-27). The Choir Director specifies, for each song of an event, the number of singers needed for each vocal part and the number of players needed for each instrument. (FE-35) |
| UC-25 | Request Service Roster Suggestion | Choir Director | The Choir Director asks the system to suggest a service roster based on required skills, approved member skills, participation confirmations and member availability. (FE-36) |
| UC-25a | View Shortage Warning | Choir Director | «extend» UC-25. When a required vocal part, instrument or role cannot be filled, the system warns the Choir Director about the shortage. (FE-37) |
| UC-25b | Adjust Roster Manually | Choir Director | «extend» UC-25. The Choir Director optionally overrides the suggested roster by replacing, adding or removing assigned members. (FE-38) |
| UC-26 | Finalize Service Roster | Choir Director | The Choir Director confirms the service roster after the song list has been approved, locking the assignment for the event. (FE-39) |
| UC-27 | Send Assignment Notifications | Choir Director | The Choir Director notifies the selected choir members of their assignment for the event. Includes S-05. (FE-40) |
| UC-28 | Manage Practice Assignments | Choir Director | The Choir Director creates practice assignments for all members, for a selected skill group or for an individual member, specifying the songs and the deadline. (FE-41) |
| UC-29 | Evaluate Practice Submission | Choir Director | The Choir Director listens to the practice audio submitted by a member, grades it as Passed or Needs Revision and writes feedback on the submission. (FE-42, FE-43, FE-44) |
| UC-30 | Take Attendance & Track Progress | Choir Director | The Choir Director records attendance at rehearsals and preparation sessions, and tracks practice progress and attendance ahead of each event. (FE-45, FE-46) |

## E. Admin — Web System (FE-47…FE-54)

| ID | Use Case | Actors | Use Case Description |
|---|---|---|---|
| UC-31 | Manage User Accounts & Roles | Admin | The Admin creates, updates and deactivates user accounts, assigns system roles (Parish Priest/Liturgy Committee, Choir Director, Choir Member, Admin) and maintains choir member profiles (personal information and membership status Active / Inactive / Left). (FE-47, FE-48) |
| UC-32 | Configure System Categories | Admin | The Admin configures the master data of the system, including skill categories, liturgical seasons, Mass types, ceremony types and event categories. (FE-49, FE-50) |
| UC-33 | Manage System Settings | Admin | The Admin maintains the general system settings that control how the system behaves for all users. (FE-51) |
| UC-34 | View Reports | Admin | The Admin views reports on user activity, attendance, participation confirmation and assignment completion. (FE-52) |
| UC-34E | Export Reports | Admin | «extend» UC-34. The Admin optionally exports the report currently being viewed, by month, by event or by liturgical season. (FE-53) |
| UC-35 | View Activity History / Audit Log | Admin | The Admin reviews the log of important activities such as role changes, skill approvals, song list approvals, roster confirmations, attendance updates and deleted materials. (FE-54) |

## Old → canonical numbering map

For cross-checking against the older `Harmonia_UseCase_Descriptions.docx` (UC-01…UC-44).

| Old | New | Old | New | Old | New | Old | New |
|---|---|---|---|---|---|---|---|
| UC-01 | S-01 | UC-12 | UC-07 | UC-23 | UC-17 | UC-34 | UC-26 |
| UC-02 | S-02 | UC-13 | UC-07E | UC-24 | UC-18 | UC-35 | UC-27 |
| UC-03 | S-03 | UC-14 | UC-08 | UC-25 | UC-19 | UC-36 | UC-28 |
| UC-04 | S-04 | UC-15 | UC-09 | UC-26 | UC-20 | UC-37 | UC-29 |
| UC-05 | S-05 | UC-16 | UC-10 | UC-27 | UC-21 | UC-38 | UC-30 |
| UC-06 | UC-01 | UC-17 | UC-11 | UC-28 | UC-22 | UC-39 | UC-31 |
| UC-07 | UC-02 | UC-18 | UC-12 | UC-29 | UC-23 | UC-40 | UC-32 |
| UC-08 | UC-03 | UC-19 | UC-13 | UC-30 | UC-24 | UC-41 | UC-33 |
| UC-09 | UC-04 | UC-20 | UC-14 | UC-31 | UC-25 | UC-42 | UC-34 |
| UC-10 | UC-05 | UC-21 | UC-15 | UC-32 | UC-25a | UC-43 | UC-34E |
| UC-11 | UC-06 | UC-22 | UC-16 | UC-33 | UC-25b | UC-44 | UC-35 |
