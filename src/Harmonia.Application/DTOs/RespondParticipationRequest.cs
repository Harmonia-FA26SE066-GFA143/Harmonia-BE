using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class RespondParticipationRequest
{
    public ParticipationStatus Status { get; set; }

    public string? Note { get; set; }
}