using System;
using System.Collections.Generic;

namespace Harmonia.Application.DTOs;

public class CreateRehearsalRequest
{
    /// <summary>A choir the calling director leads (D8); when <see cref="EventId"/> is set it must serve that event.</summary>
    public Guid ChoirId { get; set; }

    public Guid? EventId { get; set; }

    public Guid? LocationId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Note { get; set; }

    public List<UpdateRehearsalSongRequest> Songs { get; set; } = [];
}