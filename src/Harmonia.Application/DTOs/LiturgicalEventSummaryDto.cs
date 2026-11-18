using Harmonia.Domain.Enums;
using System;

namespace Harmonia.Application.DTOs;

public class LiturgicalEventSummaryDto
{
    public Guid Id { get; set; }

    public DateOnly EventDate { get; set; }

    public TimeOnly Time { get; set; }

    public string? Title { get; set; }

    public string LocationName { get; set; } = string.Empty;

    public EventStatus Status { get; set; }
}