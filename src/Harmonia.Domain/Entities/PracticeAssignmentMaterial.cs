using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

/// <summary>One material attached to a practice assignment, e.g. the sheet music and the sample audio of its song.</summary>
public class PracticeAssignmentMaterial : BaseEntity
{
    public Guid PracticeAssignmentId { get; set; }

    public Guid MaterialId { get; set; }

    /// <summary>Order the director attached them in, from 0.</summary>
    public int DisplayOrder { get; set; }

    public PracticeAssignment PracticeAssignment { get; set; } = null!;

    public MusicMaterial Material { get; set; } = null!;
}
