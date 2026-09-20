using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class SongClassification : BaseEntity
{
    public Guid SongId { get; set; }

    public ClassificationTarget TargetType { get; set; }

    // Points at LiturgicalSeason / MassType / CeremonyType / SongTheme depending on
    // TargetType, so no real foreign key can be declared.
    public Guid TargetId { get; set; }

    public Song Song { get; set; } = null!;
}
