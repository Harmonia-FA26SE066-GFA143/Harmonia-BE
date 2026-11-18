using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class Skill : BaseEntity
{
    public Guid CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Only members of this gender may declare the skill (Soprano → Female); null lets everyone declare it.</summary>
    public Gender? Gender { get; set; }

    public bool IsActive { get; set; } = true;

    public SkillCategory Category { get; set; } = null!;

    public ICollection<MemberSkill> MemberSkills { get; set; } = [];

    public ICollection<SongVocalRequirement> SongVocalRequirements { get; set; } = [];

    public ICollection<SongInstrumentRequirement> SongInstrumentRequirements { get; set; } = [];

    public ICollection<SongPersonnelRequirement> SongPersonnelRequirements { get; set; } = [];

    public ICollection<RosterAssignment> RosterAssignments { get; set; } = [];
}
