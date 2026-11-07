using Harmonia.Domain.Enums;
using System;

namespace Harmonia.Application.DTOs;

public class EventParticipationDto
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid MemberId { get; set; }

    public string MemberFullName { get; set; } = string.Empty;

    public ParticipationStatus Status { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public string? Note { get; set; }
}