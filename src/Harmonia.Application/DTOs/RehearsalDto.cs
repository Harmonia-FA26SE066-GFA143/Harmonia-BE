using System;
using System.Collections.Generic;

namespace Harmonia.Application.DTOs;

public class RehearsalDto
{
    public Guid Id { get; set; }

    public Guid? EventId { get; set; }

    public Guid? LocationId { get; set; }

    public string? LocationName { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Note { get; set; }

    public List<RehearsalSongDto> Songs { get; set; } = [];
}