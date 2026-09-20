using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class PracticeAssignmentTarget : BaseEntity
{
    public Guid PracticeAssignmentId { get; set; }

    public TargetType TargetType { get; set; }

    public Guid? MemberId { get; set; }

    public Guid? SkillId { get; set; }

    public PracticeAssignment PracticeAssignment { get; set; } = null!;

    public MemberProfile? Member { get; set; }

    public Skill? Skill { get; set; }
}
