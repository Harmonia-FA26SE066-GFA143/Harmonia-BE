using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>Create / update body of a skill (UC-32 / FE-49).</summary>
public class SaveSkillRequest
{
    public Guid CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Gender allowed to declare the skill; null for everyone.</summary>
    public Gender? Gender { get; set; }

    public bool IsActive { get; set; } = true;
}
