using System.Reflection;
using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Data;

public class HarmoniaDbContext(DbContextOptions<HarmoniaDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<SkillCategory> SkillCategories => Set<SkillCategory>();

    public DbSet<Skill> Skills => Set<Skill>();

    public DbSet<MemberSkill> MemberSkills => Set<MemberSkill>();

    public DbSet<LiturgicalWeek> LiturgicalWeeks => Set<LiturgicalWeek>();

    public DbSet<LiturgicalEvent> LiturgicalEvents => Set<LiturgicalEvent>();

    public DbSet<LiturgicalSeason> LiturgicalSeasons => Set<LiturgicalSeason>();

    public DbSet<MassType> MassTypes => Set<MassType>();

    public DbSet<CeremonyType> CeremonyTypes => Set<CeremonyType>();

    public DbSet<EventCategory> EventCategories => Set<EventCategory>();

    public DbSet<WorshipLocation> WorshipLocations => Set<WorshipLocation>();

    public DbSet<Song> Songs => Set<Song>();

    public DbSet<SongTheme> SongThemes => Set<SongTheme>();

    public DbSet<SongClassification> SongClassifications => Set<SongClassification>();

    public DbSet<SongVocalRequirement> SongVocalRequirements => Set<SongVocalRequirement>();

    public DbSet<SongInstrumentRequirement> SongInstrumentRequirements => Set<SongInstrumentRequirement>();

    public DbSet<MusicMaterial> MusicMaterials => Set<MusicMaterial>();

    public DbSet<MaterialLearningProgress> MaterialLearningProgresses => Set<MaterialLearningProgress>();

    public DbSet<SongList> SongLists => Set<SongList>();

    public DbSet<SongListItem> SongListItems => Set<SongListItem>();

    public DbSet<LiturgicalSlot> LiturgicalSlots => Set<LiturgicalSlot>();

    public DbSet<SongListReview> SongListReviews => Set<SongListReview>();

    public DbSet<EventParticipation> EventParticipations => Set<EventParticipation>();

    public DbSet<SongPersonnelRequirement> SongPersonnelRequirements => Set<SongPersonnelRequirement>();

    public DbSet<ServiceRoster> ServiceRosters => Set<ServiceRoster>();

    public DbSet<RosterAssignment> RosterAssignments => Set<RosterAssignment>();

    public DbSet<RosterShortage> RosterShortages => Set<RosterShortage>();

    public DbSet<Rehearsal> Rehearsals => Set<Rehearsal>();

    public DbSet<RehearsalAttendance> RehearsalAttendances => Set<RehearsalAttendance>();

    public DbSet<PracticeAssignment> PracticeAssignments => Set<PracticeAssignment>();

    public DbSet<PracticeAssignmentTarget> PracticeAssignmentTargets => Set<PracticeAssignmentTarget>();

    public DbSet<PracticeSubmission> PracticeSubmissions => Set<PracticeSubmission>();

    public DbSet<PracticeFeedback> PracticeFeedbacks => Set<PracticeFeedback>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();

    public DbSet<DirectorNote> DirectorNotes => Set<DirectorNote>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<ReportExport> ReportExports => Set<ReportExport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
