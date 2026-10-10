using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

/// <summary>One song on the programme of a rehearsal. Unlike a SongList it has no version, review or liturgical slot.</summary>
public class RehearsalSong : BaseEntity
{
    public Guid RehearsalId { get; set; }

    public Guid SongId { get; set; }

    public int DisplayOrder { get; set; }

    public string? Note { get; set; }

    public Rehearsal Rehearsal { get; set; } = null!;

    public Song Song { get; set; } = null!;
}
