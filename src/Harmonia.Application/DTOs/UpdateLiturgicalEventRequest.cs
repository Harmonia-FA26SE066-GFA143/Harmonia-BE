using System;

namespace Harmonia.Application.DTOs;

public class UpdateLiturgicalEventRequest
{
    public DateOnly EventDate { get; set; }

    public TimeOnly Time { get; set; }

    public Guid? LiturgicalSeasonId { get; set; }

    public Guid? MassTypeId { get; set; }

    public Guid? CeremonyTypeId { get; set; }

    public Guid? CategoryId { get; set; }

    public Guid LocationId { get; set; }

    public string? Title { get; set; }

    public string? SpecialRequirements { get; set; }
}
