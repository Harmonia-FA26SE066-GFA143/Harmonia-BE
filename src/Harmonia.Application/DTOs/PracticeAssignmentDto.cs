using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class PracticeAssignmentDto
{
    public Guid Id { get; set; }

    public Guid? EventId { get; set; }

    public Guid? SongId { get; set; }

    public Guid? MaterialId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Instruction { get; set; }

    public AssignmentScope Scope { get; set; }

    public DateTime DueDate { get; set; }

    public List<Guid> SkillIds { get; set; } = [];

    public List<Guid> MemberIds { get; set; } = [];
}
