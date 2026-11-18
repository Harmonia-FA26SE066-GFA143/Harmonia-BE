using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class EventParticipation : BaseEntity
{
    public Guid EventId { get; set; }

    public Guid MemberId { get; set; }

    public ParticipationStatus Status { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public string? Note { get; set; }

    /// <summary>When the member was last reminded to answer, by hand or by the daily job; spaces reminders apart.</summary>
    public DateTime? LastReminderAt { get; set; }

    public LiturgicalEvent LiturgicalEvent { get; set; } = null!;

    public MemberProfile Member { get; set; } = null!;
}
