using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class LiturgicalSeason : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? ColorHex { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<LiturgicalWeek> LiturgicalWeeks { get; set; } = [];
}
