# Harmonia – Domain Entity List & Attributes (for ERD, Report 3/4)

42 entities derived from FE-01→FE-54 (`claude/functional-requirements-by-actor.md`), the canonical use cases (`claude/use-case-list.md`) and the weekly-program model (`claude/weekly-liturgical-program-design.md`). Diagram: `harmonia-erd.mmd`.

Conceptual/logical level: attribute names, types and constraints. No indexes, no migration syntax.

## Modelling decisions

| # | Decision | Date | Effect |
|---|---|---|---|
| D1 | **No Instrumentalist role.** An instrument player is a Choir Member holding an approved `MemberSkill` with `SkillCategory = Instrument`. Rostering works off `Skill`, never role. | 2026-09-19 | `Role` has 4 values |
| D2 | **One account = one role.** | 2026-09-20 | `UserRole` dropped; `roleId` on `User` |
| D3 | **Several worship sites.** | 2026-09-20 | `WorshipLocation` is an entity; event key = `(eventDate, time, locationId)` |
| D4 | **A rejected song list is superseded by a new version**; old versions stay as history. | 2026-09-20 | `LiturgicalEvent` 1—n `SongList`, current = highest `version` |
| D5 | **Admin configures FE-49/FE-50 categories at runtime.** | 2026-09-20 | 9 lookup entities stay entities, not enums |

## Attribute conventions

Written once here instead of repeated 42 times:

- **`BaseEntity`** — every entity has `id` (Guid, PK). Swap to `int` if the team prefers; nothing else changes.
- **`BaseAuditableEntity`** — entities marked ***auditable*** also carry `createdAt`, `createdBy`, `updatedAt`, `updatedBy`. Matches `Harmonia.Domain/Common/`.
- **Lookup entities** carry `isActive` (bool) — Admin disables instead of deleting, so historical rows keep pointing at something.
- **FK naming**: `<entity>Id`. `?` after a type = nullable/optional.
- Statuses are **enums** (listed at the bottom), not entities.
- Skipped: soft delete (`isDeleted`) everywhere — `isActive` on lookups + `AuditLog` already cover it. Add when a real "restore deleted record" requirement shows up.

---

## A. Identity & Access

**1. `User`** — login account, exactly one role (D2) ***auditable***
`email` string(256) unique · `passwordHash` string · `roleId` Guid → Role · `isActive` bool = true · `lastLoginAt` DateTime?

**2. `Role`** — 4 seeded roles: Admin, ParishPriest, ChoirDirector, ChoirMember (D1)
`name` string(50) unique · `description` string(300)?

**3. ⚪ `MemberProfile`** — choir member record ***auditable***
`userId` Guid → User (unique, 1–1) · `fullName` string(100) · `phone` string(20)? · `dateOfBirth` DateOnly? · `joinedDate` DateOnly · `status` MemberStatus

**4. ⚪ `RefreshToken`** — session refresh + mobile push token
`userId` Guid → User · `tokenHash` string(500) unique · `expiresAt` DateTime · `revokedAt` DateTime? · `deviceId` string(100)? · `platform` DevicePlatform?

⚪ `MemberProfile`: merge into `User` unless member records without a login are needed.

## B. Skills

**5. `SkillCategory`** — Vocal / Instrument / Solo / Psalm / Conducting support (FE-49)
`name` string(50) unique · `description` string(300)? · `isActive` bool

**6. `Skill`** — Soprano, Alto, Tenor, Bass, Guitar, Organ, Solo, Psalmist
`categoryId` Guid → SkillCategory · `name` string(50) · `description` string(300)? · `isActive` bool · unique `(categoryId, name)`

**7. `MemberSkill`** — declared skill + approval outcome (FE-03/04/25)
`memberId` Guid → MemberProfile · `skillId` Guid → Skill · `level` SkillLevel? · `status` ApprovalStatus · `declaredAt` DateTime · `approvedBy` Guid? → User · `approvedAt` DateTime? · `rejectReason` string(500)? · unique `(memberId, skillId)`

## C. Liturgical calendar

**8. `LiturgicalWeek`** — weekly program, Monday → Sunday (FE-15/16a) ***auditable***
`weekStartDate` DateOnly unique (Monday) · `weekEndDate` DateOnly (= start + 6) · `liturgicalSeasonId` Guid? → LiturgicalSeason · `status` PublishStatus · `publishedAt` DateTime?

**9. `LiturgicalEvent`** — one Mass / ceremony / special event (FE-16) ***auditable***
`weekId` Guid → LiturgicalWeek · `eventDate` DateOnly · `time` TimeOnly · `massTypeId` Guid? → MassType · `ceremonyTypeId` Guid? → CeremonyType · `categoryId` Guid? → EventCategory · `locationId` Guid → WorshipLocation · `title` string(200)? · `specialRequirements` string(1000)? · `status` EventStatus · unique `(eventDate, time, locationId)` (D3)

**10. `LiturgicalSeason`** — Advent, Lent, Ordinary Time… (FE-50)
`name` string(100) · `startDate` DateOnly · `endDate` DateOnly · `colorHex` string(7)? · `isActive` bool

**11. `MassType`** · **12. `CeremonyType`** · **13. `EventCategory`** — same shape (FE-50)
`name` string(100) unique · `description` string(300)? · `isActive` bool

**14. `WorshipLocation`** — main church, chapel… (D3)
`name` string(100) unique · `address` string(300)? · `isActive` bool

## D. Music library

**15. `Song`** — song in the library (FE-27) ***auditable***
`title` string(200) · `composer` string(150)? · `lyricist` string(150)? · `musicalKey` string(10)? · `tempo` string(50)? · `notes` string(1000)? · `isActive` bool

**16. `SongTheme`** — theme taxonomy (FE-29/50)
`name` string(100) unique · `description` string(300)? · `isActive` bool

**17. `SongClassification`** — song ↔ season / Mass type / ceremony type / theme (FE-29)
`songId` Guid → Song · `targetType` ClassificationTarget · `targetId` Guid · unique `(songId, targetType, targetId)`

**18. `SongVocalRequirement`** · **19. `SongInstrumentRequirement`** — skills the song needs (FE-29)
`songId` Guid → Song · `skillId` Guid → Skill · `isMandatory` bool · unique `(songId, skillId)`

**20. `MusicMaterial`** — sheet music / lyrics / sample audio / rehearsal material (FE-08/28) ***auditable***
`songId` Guid → Song · `materialType` MaterialType · `title` string(200) · `fileUrl` string(500) · `fileName` string(200) · `fileSizeBytes` long? · `targetSkillId` Guid? → Skill · `isActive` bool

**21. `MaterialLearningProgress`** — Learned / Needs practice (FE-09)
`memberId` Guid → MemberProfile · `materialId` Guid → MusicMaterial · `status` LearningStatus · `updatedAt` DateTime · unique `(memberId, materialId)`

`SongVocalRequirement` + `SongInstrumentRequirement` can be one `SongSkillRequirement` — `SkillCategory` already separates vocal from instrument.

## E. Song list & approval

**22. `SongList`** — song list of one event, one row per version (D4) ***auditable***
`eventId` Guid → LiturgicalEvent · `version` int (from 1) · `status` SongListStatus · `proposedBy` Guid → User · `previousVersionId` Guid? → SongList · `submittedAt` DateTime? · `decidedAt` DateTime? · unique `(eventId, version)`

**23. `SongListItem`** — one song at one liturgical slot
`songListId` Guid → SongList · `songId` Guid → Song · `slotId` Guid → LiturgicalSlot · `displayOrder` int · `note` string(500)?

**24. `LiturgicalSlot`** — Entrance / Responsorial Psalm / Offertory / Communion / Recessional (D5)
`name` string(100) unique · `defaultOrder` int · `isActive` bool

**25. `SongListReview`** — priest's decision + notes (FE-17/18/19)
`songListId` Guid → SongList · `reviewerId` Guid → User · `decision` ReviewDecision · `notes` string(1000)? · `reviewedAt` DateTime

**Versioning rules (D4)**
- Current list of an event = highest `version`; older rows are frozen, never edited.
- Rejected / Needs-revision → director creates `version + 1` with `previousVersionId` pointing back.
- `SongListReview` attaches to the version it judged, so the decision trail stays readable.
- At most one version per event may be `Approved`.

## F. Participation & service roster

**26. `EventParticipation`** — request + member response (FE-06/33/34)
`eventId` Guid → LiturgicalEvent · `memberId` Guid → MemberProfile · `status` ParticipationStatus · `requestedAt` DateTime · `respondedAt` DateTime? · `note` string(300)? · unique `(eventId, memberId)`

**27. `SongPersonnelRequirement`** — people needed per song of an event (FE-35)
`songListItemId` Guid → SongListItem · `skillId` Guid → Skill · `requiredCount` int · unique `(songListItemId, skillId)`

**28. `ServiceRoster`** — roster of one event (FE-36/39) ***auditable***
`eventId` Guid → LiturgicalEvent (unique, 1–1) · `status` RosterStatus · `generatedAt` DateTime? · `generatedBy` Guid? → User · `finalizedAt` DateTime? · `finalizedBy` Guid? → User

**29. `RosterAssignment`** — one assignment line (FE-38/40)
`rosterId` Guid → ServiceRoster · `memberId` Guid → MemberProfile · `skillId` Guid → Skill · `songListItemId` Guid? → SongListItem · `source` AssignmentSource · `notifiedAt` DateTime?

**30. ⚪ `RosterShortage`** — shortage warning (FE-37)
`rosterId` Guid → ServiceRoster · `skillId` Guid → Skill · `requiredCount` int · `availableCount` int · `detectedAt` DateTime

⚪ Compute at runtime instead of storing, unless warning history matters.

## G. Rehearsal & practice

**31. `Rehearsal`** — rehearsal session (FE-26) ***auditable***
`eventId` Guid? → LiturgicalEvent · `locationId` Guid? → WorshipLocation · `startTime` DateTime · `endTime` DateTime · `note` string(500)?

**32. `RehearsalAttendance`** — attendance record (FE-45/46)
`rehearsalId` Guid → Rehearsal · `memberId` Guid → MemberProfile · `status` AttendanceStatus · `checkedBy` Guid → User · `checkedAt` DateTime · unique `(rehearsalId, memberId)`

**33. `PracticeAssignment`** — practice task from the director (FE-41) ***auditable***
`eventId` Guid? → LiturgicalEvent · `songId` Guid? → Song · `materialId` Guid? → MusicMaterial · `title` string(200) · `instruction` string(1000)? · `scope` AssignmentScope · `dueDate` DateTime

**34. ⚪ `PracticeAssignmentTarget`** — resolves scope All / SkillGroup / Individual
`practiceAssignmentId` Guid → PracticeAssignment · `targetType` TargetType · `memberId` Guid? → MemberProfile · `skillId` Guid? → Skill

**35. `PracticeSubmission`** — member's recorded audio (FE-11/12)
`practiceAssignmentId` Guid → PracticeAssignment · `memberId` Guid → MemberProfile · `audioUrl` string(500) · `durationSeconds` int? · `attemptNo` int (from 1) · `submittedAt` DateTime · `status` SubmissionStatus

**36. ⚪ `PracticeFeedback`** — director's evaluation (FE-13/43/44)
`submissionId` Guid → PracticeSubmission · `reviewerId` Guid → User · `result` SubmissionStatus (Passed / NeedsRevision only) · `comment` string(1000)? · `reviewedAt` DateTime

⚪ `PracticeAssignmentTarget`: unnecessary if assignments always go to everyone.
⚪ `PracticeFeedback`: merge into `PracticeSubmission` when one submission carries at most one review — `result` then just mirrors `status`.

## H. Communication

**37. `Notification`** — system notification (S-05, scaffolded in `Domain/Entities`)
`type` NotificationType · `title` string(200) · `content` string(1000) · `referenceType` string(50)? · `referenceId` Guid? · `createdAt` DateTime

**38. ⚪ `NotificationRecipient`** — recipient + read state
`notificationId` Guid → Notification · `userId` Guid → User · `isRead` bool = false · `readAt` DateTime? · unique `(notificationId, userId)`

**39. ⚪ `DirectorNote`** — priest → director note about a week/event (FE-23)
`weekId` Guid? → LiturgicalWeek · `eventId` Guid? → LiturgicalEvent · `fromUserId` Guid → User · `toUserId` Guid → User · `content` string(2000) · `sentAt` DateTime

⚪ `NotificationRecipient`: needed only for broadcast notifications.
⚪ `DirectorNote`: reuse `Notification` if no reply thread is required.

## I. System

**40. `SystemSetting`** — key–value settings (FE-51)
`key` string(100) unique · `value` string(1000) · `dataType` SettingDataType · `description` string(300)?

**41. `AuditLog`** — important activity history (FE-54)
`userId` Guid? → User · `action` string(100) · `entityType` string(100) · `entityId` Guid? · `oldValue` string? (JSON) · `newValue` string? (JSON) · `ipAddress` string(45)? · `createdAt` DateTime

**42. ⚪ `ReportExport`** — record of exported report files (FE-53)
`reportType` ReportType · `parameters` string(1000) (JSON) · `fileUrl` string(500) · `exportedBy` Guid → User · `exportedAt` DateTime

⚪ Skip if reports are generated and downloaded on the fly.

---

## Enums

| Enum | Values |
|---|---|
| `MemberStatus` | Active, Inactive, Left |
| `DevicePlatform` | Android, iOS, Web |
| `SkillLevel` | Beginner, Intermediate, Advanced |
| `ApprovalStatus` | Pending, Approved, Rejected |
| `PublishStatus` | Draft, Published |
| `EventStatus` | Draft, Published, Cancelled |
| `ClassificationTarget` | LiturgicalSeason, MassType, CeremonyType, SongTheme |
| `MaterialType` | SheetMusic, Lyrics, SampleAudio, RehearsalMaterial |
| `LearningStatus` | NotStarted, NeedsPractice, Learned |
| `SongListStatus` | Draft, Submitted, Approved, Rejected, NeedsRevision |
| `ReviewDecision` | Approve, Reject, RequestRevision |
| `ParticipationStatus` | Invited, Confirmed, Declined, Unsure |
| `RosterStatus` | Draft, Suggested, Finalized |
| `AssignmentSource` | Suggested, Manual |
| `AttendanceStatus` | Present, Absent, Late, Excused |
| `AssignmentScope` | All, SkillGroup, Individual |
| `TargetType` | Member, Skill |
| `SubmissionStatus` | Submitted, Passed, NeedsRevision, Overdue |
| `NotificationType` | WeekPublished, SongListDecision, ParticipationRequest, AssignmentNotice, PracticeFeedback, DirectorNote |
| `SettingDataType` | String, Int, Bool, Json |
| `ReportType` | UserActivity, Attendance, Participation, AssignmentCompletion, SongUsage, ServiceHistory |

21 enums. `PracticeFeedback.result` reuses `SubmissionStatus` rather than adding a near-duplicate enum.

---

## Relationships for the ERD

**Identity**
- `Role` 1 — n `User` *(D2)*
- `User` 1 — 1 `MemberProfile`
- `User` 1 — n `RefreshToken`

**Skills**
- `SkillCategory` 1 — n `Skill`
- `MemberProfile` 1 — n `MemberSkill` n — 1 `Skill`
- `MemberSkill` n — 1 `User` *(approvedBy)*

**Calendar**
- `LiturgicalSeason` 1 — n `LiturgicalWeek` 1 — n `LiturgicalEvent`
- `LiturgicalEvent` n — 1 `MassType` / `CeremonyType` / `EventCategory` / `WorshipLocation`

**Music library**
- `Song` 1 — n `SongClassification` → `LiturgicalSeason` | `MassType` | `CeremonyType` | `SongTheme`
- `Song` 1 — n `SongVocalRequirement` n — 1 `Skill`
- `Song` 1 — n `SongInstrumentRequirement` n — 1 `Skill`
- `Song` 1 — n `MusicMaterial` 1 — n `MaterialLearningProgress` n — 1 `MemberProfile`

**Song list**
- `LiturgicalEvent` 1 — n `SongList` *(D4: one row per version)*, `SongList` 0..1 — 1 `SongList` *(previousVersionId)*
- `SongList` 1 — n `SongListItem` n — 1 `Song`, n — 1 `LiturgicalSlot`
- `SongList` 1 — n `SongListReview` n — 1 `User`

**Participation & roster**
- `LiturgicalEvent` 1 — n `EventParticipation` n — 1 `MemberProfile`
- `SongListItem` 1 — n `SongPersonnelRequirement` n — 1 `Skill`
- `LiturgicalEvent` 1 — 1 `ServiceRoster` 1 — n `RosterAssignment`
- `RosterAssignment` n — 1 `MemberProfile`, n — 1 `Skill`, n — 0..1 `SongListItem`
- `ServiceRoster` 1 — n `RosterShortage` n — 1 `Skill`

**Rehearsal & practice**
- `LiturgicalEvent` 1 — n `Rehearsal` n — 1 `WorshipLocation`
- `Rehearsal` 1 — n `RehearsalAttendance` n — 1 `MemberProfile`
- `PracticeAssignment` n — 0..1 `LiturgicalEvent` / `Song` / `MusicMaterial`
- `PracticeAssignment` 1 — n `PracticeAssignmentTarget` → `MemberProfile` | `Skill`
- `PracticeAssignment` 1 — n `PracticeSubmission` n — 1 `MemberProfile`
- `PracticeSubmission` 1 — n `PracticeFeedback` n — 1 `User`

**Communication & system**
- `Notification` 1 — n `NotificationRecipient` n — 1 `User`
- `DirectorNote` n — 1 `LiturgicalWeek` / `LiturgicalEvent`, n — 1 `User` (from), n — 1 `User` (to)
- `AuditLog` n — 1 `User`

## Reading the diagram

- **Hubs** (most edges, put them in the middle): `LiturgicalEvent`, `MemberProfile`, `Song`, `Skill`.
- **Associative** (n–n resolvers): `MemberSkill`, `SongClassification`, `SongVocalRequirement`, `SongInstrumentRequirement`, `MaterialLearningProgress`, `SongPersonnelRequirement`, `RosterAssignment`, `PracticeAssignmentTarget`, `NotificationRecipient`.
- **Lookup** (Admin-configured per D5, leaves): `Role`, `SkillCategory`, `LiturgicalSeason`, `MassType`, `CeremonyType`, `EventCategory`, `WorshipLocation`, `SongTheme`, `LiturgicalSlot`.
- **Standalone** (no FK into the core): `SystemSetting`, `AuditLog`, `ReportExport`.

## Source & history
- 2026-09-19: created from FE-01→FE-54 + the canonical use case list (D1).
- 2026-09-20: D2–D5 applied — `UserRole` removed (43 → 42); `WorshipLocation`, `EventCategory`, `SongTheme`, `LiturgicalSlot` promoted to core; song-list versioning rules added.
- 2026-09-20: attributes expanded to typed form with conventions + 21 enums.
