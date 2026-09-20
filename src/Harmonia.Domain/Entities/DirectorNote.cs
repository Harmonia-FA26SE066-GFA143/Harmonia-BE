using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class DirectorNote : BaseEntity
{
    public Guid? WeekId { get; set; }

    public Guid? EventId { get; set; }

    public Guid FromUserId { get; set; }

    public Guid ToUserId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }

    public LiturgicalWeek? Week { get; set; }

    public LiturgicalEvent? LiturgicalEvent { get; set; }

    public User FromUser { get; set; } = null!;

    public User ToUser { get; set; } = null!;
}
