using Harmonia.Domain.Enums;
using System;

namespace Harmonia.Application.DTOs;

/// <summary>The calling member's own participation request for one event (UC-04 / UC-05).</summary>
public class MyParticipationDto
{
    public Guid ParticipationId { get; set; }

    public Guid EventId { get; set; }

    public ParticipationStatus Status { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public string? Note { get; set; }

    /// <summary>The answer is final, so this is true only while the request is still Invited and the event has not started.</summary>
    public bool CanRespond { get; set; }

    /// <summary>The error code PUT mine/{eventId} would return; null when <see cref="CanRespond"/> is true.</summary>
    public string? CannotRespondReason { get; set; }
}
