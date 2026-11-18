namespace Harmonia.Application.DTOs;

/// <summary>An active choir as the anonymous join form lists it: no director, no member count.</summary>
public class ChoirSummaryDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
