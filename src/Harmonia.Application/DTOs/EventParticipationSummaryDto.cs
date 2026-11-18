using System;
using System.Collections.Generic;

namespace Harmonia.Application.DTOs;

public class EventParticipationSummaryDto
{
    public Guid EventId { get; set; }

    public int TotalCount { get; set; }

    public int InvitedCount { get; set; }

    public int ConfirmedCount { get; set; }

    public int DeclinedCount { get; set; }

    public int UnsureCount { get; set; }

    public List<EventParticipationDto> Items { get; set; } = [];
}