using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class CreatePracticeAssignmentRequest
{
    public Guid? EventId { get; set; }

    public Guid? SongId { get; set; }

    /// <summary>When <see cref="SongId"/> is also set, the material must belong to that song.</summary>
    public Guid? MaterialId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Instruction { get; set; }

    public AssignmentScope Scope { get; set; }

    /// <summary>UTC.</summary>
    public DateTime DueDate { get; set; }

    /// <summary>Required when <see cref="Scope"/> is SkillGroup; ignored otherwise.</summary>
    public List<Guid>? SkillIds { get; set; }

    /// <summary>Member profile ids. Required when <see cref="Scope"/> is Individual; ignored otherwise.</summary>
    public List<Guid>? MemberIds { get; set; }
}
