using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class DirectorNominationDto
{
    public Guid Id { get; set; }

    public Guid MemberId { get; set; }

    public Guid MemberUserId { get; set; }

    public string MemberFullName { get; set; } = string.Empty;

    public Guid NominatedById { get; set; }

    public string NominatedByFullName { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public Guid? ProposedChoirId { get; set; }

    public string? ProposedChoirName { get; set; }

    public DirectorNominationStatus Status { get; set; }

    public Guid? ReviewedById { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewNote { get; set; }

    public string? TrainingWaiverReason { get; set; }

    public Guid? AppointedChoirId { get; set; }

    public string? AppointedChoirName { get; set; }

    public DateTime? AppointedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
