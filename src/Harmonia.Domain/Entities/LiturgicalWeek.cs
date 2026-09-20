using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class LiturgicalWeek : BaseAuditableEntity
{
    public DateOnly WeekStartDate { get; set; }

    public DateOnly WeekEndDate { get; set; }

    public Guid? LiturgicalSeasonId { get; set; }

    public PublishStatus Status { get; set; }

    public DateTime? PublishedAt { get; set; }

    public LiturgicalSeason? LiturgicalSeason { get; set; }

    public ICollection<LiturgicalEvent> LiturgicalEvents { get; set; } = [];

    public ICollection<DirectorNote> DirectorNotes { get; set; } = [];
}
