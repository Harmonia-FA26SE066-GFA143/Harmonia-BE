using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class SkillCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Skill> Skills { get; set; } = [];
}
