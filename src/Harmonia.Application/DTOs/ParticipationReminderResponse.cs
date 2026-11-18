namespace Harmonia.Application.DTOs;

public class ParticipationReminderResponse
{
    /// <summary>Members reminded by this call.</summary>
    public int RemindedCount { get; set; }

    /// <summary>Active members still Invited, including those skipped because they were reminded recently.</summary>
    public int PendingCount { get; set; }
}
