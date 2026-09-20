using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class PracticeAssignment : BaseAuditableEntity
{
    public Guid? EventId { get; set; }

    public Guid? SongId { get; set; }

    public Guid? MaterialId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Instruction { get; set; }

    public AssignmentScope Scope { get; set; }

    public DateTime DueDate { get; set; }

    public LiturgicalEvent? LiturgicalEvent { get; set; }

    public Song? Song { get; set; }

    public MusicMaterial? Material { get; set; }

    public ICollection<PracticeAssignmentTarget> Targets { get; set; } = [];

    public ICollection<PracticeSubmission> Submissions { get; set; } = [];
}
