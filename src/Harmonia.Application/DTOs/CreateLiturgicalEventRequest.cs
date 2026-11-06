using System;
using System.Collections.Generic;

namespace Harmonia.Application.DTOs;

public class CreateLiturgicalEventRequest
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

    /// <summary>The choirs serving the event (D7); at least one.</summary>
    public List<Guid> ChoirIds { get; set; } = [];

    /// <summary>The lead choir (D8): required, and must be one of <see cref="ChoirIds"/>.</summary>
    public Guid? LeadChoirId { get; set; }
}