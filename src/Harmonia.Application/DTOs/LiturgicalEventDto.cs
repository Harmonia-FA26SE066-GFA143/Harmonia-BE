using Harmonia.Domain.Enums;
using System;

namespace Harmonia.Application.DTOs;

public class LiturgicalEventDto
{
    public Guid Id { get; set; }

    public DateOnly EventDate { get; set; }

    public TimeOnly Time { get; set; }

    public Guid? LiturgicalSeasonId { get; set; }

    public Guid? MassTypeId { get; set; }

    public Guid? CeremonyTypeId { get; set; }

    public Guid? CategoryId { get; set; }

    public Guid LocationId { get; set; }

    public string LocationName { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? SpecialRequirements { get; set; }

    public EventStatus Status { get; set; }

    public DateTime? PublishedAt { get; set; }
}