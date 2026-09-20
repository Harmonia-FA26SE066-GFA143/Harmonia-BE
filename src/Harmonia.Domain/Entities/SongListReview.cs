using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class SongListReview : BaseEntity
{
    public Guid SongListId { get; set; }

    public Guid ReviewerId { get; set; }

    public ReviewDecision Decision { get; set; }

    public string? Notes { get; set; }

    public DateTime ReviewedAt { get; set; }

    public SongList SongList { get; set; } = null!;

    public User Reviewer { get; set; } = null!;
}
