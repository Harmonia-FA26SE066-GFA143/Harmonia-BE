using System;

namespace Harmonia.Application.DTOs;

public class RehearsalSummaryDto
{
    public Guid Id { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? LocationName { get; set; }

    public string? Note { get; set; }

    public List<RehearsalSongDto> Songs { get; set; } = [];
}