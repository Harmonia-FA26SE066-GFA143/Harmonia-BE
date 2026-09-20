using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class MemberProfile : BaseAuditableEntity
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public DateOnly JoinedDate { get; set; }

    public MemberStatus Status { get; set; }

    public User User { get; set; } = null!;

    public ICollection<MemberSkill> MemberSkills { get; set; } = [];

    public ICollection<MaterialLearningProgress> LearningProgresses { get; set; } = [];

    public ICollection<EventParticipation> EventParticipations { get; set; } = [];

    public ICollection<RosterAssignment> RosterAssignments { get; set; } = [];

    public ICollection<RehearsalAttendance> RehearsalAttendances { get; set; } = [];

    public ICollection<PracticeSubmission> PracticeSubmissions { get; set; } = [];
}
