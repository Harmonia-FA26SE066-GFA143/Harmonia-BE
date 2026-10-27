using System.Reflection;
using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Harmonia.Infrastructure.Data;

public class HarmoniaDbContext(DbContextOptions<HarmoniaDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Collation keyword searches compare under: ignores case and Vietnamese diacritics ("con buoc" finds
    /// "Con Bước", "duc" finds "Đức"). Vietnamese_100_CI_AI does not: it keeps ư, ơ, đ as letters of their own.
    /// </summary>
    public const string SearchCollation = "Latin1_General_100_CI_AI";

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<SkillCategory> SkillCategories => Set<SkillCategory>();

    public DbSet<Skill> Skills => Set<Skill>();

    public DbSet<MemberSkill> MemberSkills => Set<MemberSkill>();

    public DbSet<Choir> Choirs => Set<Choir>();

    public DbSet<ChoirMembership> ChoirMemberships => Set<ChoirMembership>();

    public DbSet<LiturgicalEventChoir> LiturgicalEventChoirs => Set<LiturgicalEventChoir>();

    public DbSet<LiturgicalDay> LiturgicalDays => Set<LiturgicalDay>();

    public DbSet<LiturgicalNameTranslation> LiturgicalNameTranslations => Set<LiturgicalNameTranslation>();

    public DbSet<LiturgicalEvent> LiturgicalEvents => Set<LiturgicalEvent>();

    public DbSet<LiturgicalSeason> LiturgicalSeasons => Set<LiturgicalSeason>();

    public DbSet<MassType> MassTypes => Set<MassType>();

    public DbSet<MassSchedule> MassSchedules => Set<MassSchedule>();

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

    public DbSet<EventPersonnelRequirement> EventPersonnelRequirements => Set<EventPersonnelRequirement>();

    public DbSet<LiturgicalSlotRequirement> LiturgicalSlotRequirements => Set<LiturgicalSlotRequirement>();

    public DbSet<ServiceRoster> ServiceRosters => Set<ServiceRoster>();

    public DbSet<RosterAssignment> RosterAssignments => Set<RosterAssignment>();

    public DbSet<Rehearsal> Rehearsals => Set<Rehearsal>();

    public DbSet<RehearsalAttendance> RehearsalAttendances => Set<RehearsalAttendance>();

    public DbSet<RehearsalSong> RehearsalSongs => Set<RehearsalSong>();

    public DbSet<PracticeAssignment> PracticeAssignments => Set<PracticeAssignment>();

    public DbSet<PracticeAssignmentTarget> PracticeAssignmentTargets => Set<PracticeAssignmentTarget>();

    public DbSet<PracticeAssignmentMaterial> PracticeAssignmentMaterials => Set<PracticeAssignmentMaterial>();

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
        ApplyUtcDateTimeConverter(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Every DateTime is stored in UTC. The database keeps no time zone, so values read back are marked
    /// as UTC; otherwise they would serialize without "Z" and clients could not tell what they mean.
    /// </summary>
    private static void ApplyUtcDateTimeConverter(ModelBuilder modelBuilder)
    {
        var converter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var properties = modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?));

        foreach (var property in properties)
        {
            property.SetValueConverter(converter);
        }
    }
}
