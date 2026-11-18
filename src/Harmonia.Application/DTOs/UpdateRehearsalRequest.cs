using System;

namespace Harmonia.Application.DTOs;

/// <summary>Body of PUT api/rehearsals/{id}. Songs are edited through PUT api/rehearsals/{id}/songs.</summary>
public class UpdateRehearsalRequest
{
    public Guid? EventId { get; set; }

    public Guid? LocationId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Note { get; set; }
}
