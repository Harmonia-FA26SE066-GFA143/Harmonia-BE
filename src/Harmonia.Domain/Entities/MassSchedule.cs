using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

/// <summary>
/// A fixed weekly Mass time of the parish (e.g. Sunday 04:00). The Admin keeps the list; the priest generates
/// Draft <see cref="LiturgicalEvent"/>s from it for a date range.
/// </summary>
public class MassSchedule : BaseAuditableEntity
{
    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly Time { get; set; }

    public Guid MassTypeId { get; set; }

    public Guid LocationId { get; set; }

    public string? Title { get; set; }

    public bool IsActive { get; set; } = true;

    public MassType MassType { get; set; } = null!;

    public WorshipLocation Location { get; set; } = null!;
}
