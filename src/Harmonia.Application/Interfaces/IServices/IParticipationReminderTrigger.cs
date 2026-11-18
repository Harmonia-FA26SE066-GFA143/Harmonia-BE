namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Asks for today's participation reminders. Cheap to call on every request: only the first call of the day, from
/// 07:00 parish time, starts a run, and the run happens in the background.
/// </summary>
public interface IParticipationReminderTrigger
{
    void RequestDailyRun();
}
