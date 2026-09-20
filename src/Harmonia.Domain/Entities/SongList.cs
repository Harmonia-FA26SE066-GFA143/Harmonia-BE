using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class SongList : BaseAuditableEntity
{
    public Guid EventId { get; set; }

    public int Version { get; set; }

    public SongListStatus Status { get; set; }

    public Guid ProposedBy { get; set; }

    public Guid? PreviousVersionId { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? DecidedAt { get; set; }

    public LiturgicalEvent LiturgicalEvent { get; set; } = null!;

    public User Proposer { get; set; } = null!;

    public SongList? PreviousVersion { get; set; }

    public ICollection<SongListItem> Items { get; set; } = [];

    public ICollection<SongListReview> Reviews { get; set; } = [];
}
