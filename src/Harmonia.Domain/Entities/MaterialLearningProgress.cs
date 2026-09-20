using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class MaterialLearningProgress : BaseEntity
{
    public Guid MemberId { get; set; }

    public Guid MaterialId { get; set; }

    public LearningStatus Status { get; set; }

    public DateTime UpdatedAt { get; set; }

    public MemberProfile Member { get; set; } = null!;

    public MusicMaterial Material { get; set; } = null!;
}
