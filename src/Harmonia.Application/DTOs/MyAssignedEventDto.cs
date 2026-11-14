using System;
using System.Collections.Generic;

namespace Harmonia.Application.DTOs;

/// <summary>An upcoming event the calling member serves at, with what they were assigned to.</summary>
public class MyAssignedEventDto
{
    public Guid EventId { get; set; }

    public DateOnly EventDate { get; set; }

    public TimeOnly Time { get; set; }

    public string? Title { get; set; }

    public string LocationName { get; set; } = string.Empty;

    public List<MyAssignmentDto> Assignments { get; set; } = [];
}
