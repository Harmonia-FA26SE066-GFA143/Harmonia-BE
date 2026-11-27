namespace Harmonia.Application.DTOs;

/// <summary>One active member's answers to the participation requests of the report's range (UC-34 / FE-52).</summary>
public class ParticipationReportDto
{
    public Guid MemberId { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>Requests still unanswered.</summary>
    public int InvitedCount { get; set; }

    public int ConfirmedCount { get; set; }

    public int DeclinedCount { get; set; }

    public int UnsureCount { get; set; }
}
