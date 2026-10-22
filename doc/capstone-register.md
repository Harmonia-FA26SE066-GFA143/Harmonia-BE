# CAPSTONE PROJECT REGISTER

**Class:** &nbsp;&nbsp; **Duration time:** from 09/2026 to 03/2027
**(\*) Profession:** Software Engineer &nbsp;&nbsp; **Specialty:** SE ☒
**(\*) Kinds of person make registers:** Lecturer ☒ &nbsp; Students ☐

## 1. Register information for supervisor (if have)

| No. | Full name | Phone | E-Mail | Title |
|---|---|---|---|---|
| Supervisor | Đỗ Phúc Thịnh | | thinhdp2@fpt.edu.vn | |

## 2. Register information for students (if have)

| No. | Full name | Student code | Phone | E-mail | Role in Group |
|---|---|---|---|---|---|
| 1 | | | | | |
| 2 | | | | | |
| 3 | | | | | |
| 4 | | | | | |
| 5 | | | | | |

## 3. Register content of Capstone Project

### (\*) 3.1. Capstone Project name

- 3.1.1. English: **Harmonia - A Choir Coordination and Liturgical Music Management System**
- 3.1.2. Vietnamese: **Harmonia - Hệ thống quản lý điều phối ca đoàn và Thánh nhạc phụng vụ**

### (\*) 3.2. Main proposal content (including result and product)

#### a) Context

A church choir plays an important role in liturgical music service. Its activities include preparing suitable songs for Masses, liturgical seasons, ceremonies, and parish events. Song selection needs to follow not only musical quality but also liturgical meaning, ceremony type, and guidance from the parish priest or liturgy committee.

Choir operation also requires managing members, instrumentalists, rehearsal schedules, music resources, participation confirmation, and service rosters. Each member may have different capabilities such as Soprano, Alto, Tenor, Bass, Psalmist, Solo singing, Guitar, or Organ. Currently, many choirs still coordinate through chat groups, spreadsheets, or verbal announcements. This makes it difficult to track approved songs, member availability, rehearsal progress, and whether the choir has enough suitable members for each event. Harmonia is proposed to support both choir coordination and liturgical music management for a parish with one or more choirs. The system helps the parish priest or liturgy committee review song lists, helps the choir director prepare rehearsals and service rosters, helps choir leaders run their own choir's rehearsals, and helps members access materials, confirm participation, and submit practice recordings.

#### b) Proposed Solutions

- Manage choir members by role, vocal skill, instrument skill, and service capability.
- Manage several choirs in one parish: members may join more than one choir, and one event may be served by several choirs.
- Allow a choir leader to manage his or her choir and to run its rehearsals when the choir director is absent.
- Manage liturgical events, Masses, ceremonies, rehearsal schedules, song lists, and music resources.
- Allow choir members and instrumentalists to confirm their availability before events.
- Allow choir directors to propose song lists for Masses, ceremonies, and liturgical seasons.
- Allow the parish priest or liturgy committee to review and approve song lists before they are used.
- Manage liturgical music resources such as sheet music, lyrics, sample audio, rehearsal materials, and song classification by liturgical season.
- Support choir directors in suggesting service rosters with AI assistance (Gemini API), based on required vocal skills, instruments, approved member skills, participation confirmation, and member availability.
- Support rehearsal preparation through practice assignments, audio submissions, feedback, and attendance tracking.
- Provide reports on attendance, participation confirmation, song usage, practice progress, and service preparation.

#### c) Functional Requirements

**Choir Member / Instrumentalist Mobile App** *(instrumentalists are choir members with approved instrument skills)*

- Members can log in and view their personal profile.
- Members can view their assigned role, vocal skills, instrument skills, and approved capabilities.
- Members can declare new skills such as Soprano, Alto, Tenor, Bass, Guitar, Organ, Solo, or Psalmist.
- Members can track the approval status of each declared skill.
- Members can view upcoming Masses, ceremonies, performances, and rehearsal schedules.
- Members can confirm attendance, decline attendance, or mark themselves as unsure for each event.
- Members can view the approved song list of an event.
- Members can access sheet music, lyrics, sample audio, and rehearsal materials suitable for their assigned songs or skills.
- Members can mark music materials as learned or needing more practice.
- Members can receive practice assignments from the choir director.
- Members can record and submit practice audio through the application.
- Members can view the status of each practice submission, such as submitted, passed, needs revision, or overdue.
- Members can view feedback from the choir director.
- Members can view their participation history and practice history.

**Choir Leader Mobile App** *(a choir leader is also a choir member and keeps all member features)*

- Choir leaders can view the members of their choir, with their skills and attendance.
- Choir leaders can create, update, and cancel rehearsals for their choir.
- Choir leaders can take attendance at their choir's rehearsals when the choir director is absent.
- Choir leaders can update the song programme of their choir's rehearsals.
- Choir leaders can view the participation and practice progress of their choir for upcoming events.

**Parish Priest / Liturgy Committee Web System**

- Parish Priest can create liturgical programs day by day, based on the Catholic liturgical calendar imported from an external calendar service, and publish or cancel each event.
- Parish Priest can define basic information for each liturgical event, such as event name, date, liturgical season, Mass type, ceremony type, and special requirements.
- Parish Priest can review the song list proposed by the choir director.
- Parish Priest can approve, reject, or request revision of the proposed song list; each revision is kept as a new version.
- Parish Priest can provide notes on song suitability, such as theme, liturgical season, ceremony context, or pastoral concerns.
- Parish Priest can view the final approved song list for each event.
- Parish Priest can view the preparation status of the choir for important events.
- Parish Priest can access reports on choir service history, song usage, and event preparation.
- Parish Priest can send general notes or requests to the choir director regarding upcoming liturgical programs.

**Choir Director Web System**

- Choir directors can manage all choirs of the parish, their members, and instrumentalists.
- Choir directors can assign members to choirs and assign choirs to liturgical events.
- Choir directors can appoint a choir leader for each choir.
- Choir directors can review and approve skills declared by members.
- Choir directors can create rehearsal schedules for each event.
- Choir directors can manage the liturgical music library.
- Choir directors can upload sheet music, lyrics, sample audio, and rehearsal materials.
- Choir directors can classify songs by liturgical season, Mass type, ceremony type, theme, vocal requirement, and instrument requirement.
- Choir directors can propose a song list for a Mass, ceremony, or special event.
- Choir directors can submit the proposed song list to the parish priest or liturgy committee for approval.
- Choir directors can revise the song list based on feedback from the parish priest or liturgy committee.
- Choir directors can send participation confirmation requests to choir members and instrumentalists.
- Choir directors can view participation confirmation status in real time.
- Choir directors can define personnel requirements for each song, such as the number of singers needed for each vocal part or required instrumentalists.
- Choir directors can request service roster suggestions from the system.
- Choir directors can view shortage warnings for missing vocal skills, instruments, or required roles.
- Choir directors can manually adjust the suggested service roster.
- Choir directors can finalize the service roster after the song list has been approved.
- Choir directors can send assignment notifications to selected members.
- Choir directors can create practice assignments for all members, selected skill groups, or individual members.
- Choir directors can listen to practice audio submitted by members.
- Choir directors can evaluate submissions as Passed or Needs Revision.
- Choir directors can enter feedback for each submission.
- Choir directors can take attendance during rehearsals or service preparation sessions.
- Choir directors can track practice progress and attendance for upcoming events.

**Admin Web System**

- Admins can create user accounts (there is no public sign-up; the first password is sent by email and must be changed at first login) and assign system roles such as Parish Priest/Liturgy Committee, Choir Director, Choir Member, and Admin.
- Admins can configure skill categories, skills, liturgical seasons, Mass types, ceremony types, event categories, song themes, liturgical slots, and worship locations.
- Admins can manage general system settings.
- Admins can view reports on user activity, attendance, participation confirmation, and assignment completion.
- Admins can export reports by month, event, or liturgical season in CSV, XLSX, PDF, XML, or JSON.
- Admins can view important activity history such as role changes, skill approvals, song list approvals, roster confirmation, attendance updates, and deleted materials.

**All users**

- Users can sign in with email and password or with a Google account linked to an existing account.
- Users can reset a forgotten password by email and change their password.
- Users receive real-time notifications about skill approvals, song list submissions, event publishing and cancellation, and roster assignments.

#### d) Non-Functional Requirements

- Usability: Choir members should be able to confirm participation for an event in less than 1 minute.
- Performance: Event dashboards and participation summaries should load within 3 seconds for a medium-sized choir.
- Consistency: Participation confirmation, attendance records, and practice assignment status must be consistent across the mobile app and web system.
- Compatibility: The system should support both mobile access for members and choir leaders, and web access for the parish priest, choir directors, and admins.

#### e) Theory & Practical

- Students apply the software development process and UML 2.0 in modeling the system. Documents include: User Requirements, Software Requirement Specification (SRS), Architecture Design, Detailed Design, System Implementation, Testing Document, Installation Guide, Source Code, and Deployment Packages.
  - Server: .NET 9 (ASP.NET Core Web API, Clean Architecture), SignalR
  - Database: SQL Server
  - Web Client: *(team's web framework)*
  - Mobile App: *(team's mobile framework)*
  - AI: Gemini API (service roster suggestion)
  - Cloud services: Cloudinary (file storage), Brevo (email), external Catholic liturgical calendar API

#### f) Products (Expected Deliverables)

- Mobile App for choir members, choir leaders, and instrumentalists.
- Web System for parish priest or liturgy committee.
- Web System for choir directors.
- Web Admin for administrators.
- Web API for the system.

#### g) Proposed Tasks

- Task package 1: Analyze choir coordination and liturgical music management requirements.
- Task package 2: Identify actors, business flows, and major use cases.
- Task package 3: Design database, role permissions, and screen flows.
- Task package 4: Develop member profile, role, skill, multi-choir and choir leader management, and participation confirmation features.
- Task package 5: Develop liturgical event, Mass, ceremony, liturgical calendar integration, and rehearsal schedule management.
- Task package 6: Develop liturgical music resource management.
- Task package 7: Develop song list proposal and approval features.
- Task package 8: Develop practice assignment, audio submission, and assessment features.
- Task package 9: Develop attendance tracking, service roster suggestion, and notification features.
- Task package 10: Develop Admin Web System, reports, testing, deployment, and required documents.

## 4. Other comments (propose all relative things if have)

...

---

HCM, date 17/08/2026

| Supervisor (If have) | On behalf of Registers |
|---|---|
| *(Sign and full name)* | *(Sign and full name)* |
