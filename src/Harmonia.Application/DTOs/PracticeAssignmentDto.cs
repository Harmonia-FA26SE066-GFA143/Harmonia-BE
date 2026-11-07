using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class PracticeAssignmentDto
{
    public Guid Id { get; set; }

    /// <summary>Null on assignments created before choirs were scoped (D8).</summary>
    public Guid? ChoirId { get; set; }

    public Guid? EventId { get; set; }

    public Guid? SongId { get; set; }

    /// <summary>The first of <see cref="MaterialIds"/>, for clients that read one material.</summary>
    public Guid? MaterialId { get; set; }

    /// <summary>Every attached material in order; empty when none.</summary>
    public List<Guid> MaterialIds { get; set; } = [];

    public string Title { get; set; } = string.Empty;

    public string? Instruction { get; set; }

    public AssignmentScope Scope { get; set; }

    public DateTime DueDate { get; set; }

    public List<Guid> SkillIds { get; set; } = [];

    public List<Guid> MemberIds { get; set; } = [];
}
