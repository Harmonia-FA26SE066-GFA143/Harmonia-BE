using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>Real-time signal that a member answered an invitation; the director's page re-reads the list (B52).</summary>
public class ParticipationChangedDto
{
    public Guid EventId { get; set; }

    public Guid MemberId { get; set; }

    public ParticipationStatus Status { get; set; }
}
