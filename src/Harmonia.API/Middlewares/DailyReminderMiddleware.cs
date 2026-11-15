using Harmonia.Application.Interfaces.IServices;

namespace Harmonia.API.Middlewares;

/// <summary>
/// A signed-in request means someone is using the app and the database is awake: a good moment for today's
/// participation reminders. Anonymous calls (home page, bots) do not count.
/// </summary>
public class DailyReminderMiddleware(RequestDelegate next, IParticipationReminderTrigger trigger)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            trigger.RequestDailyRun();
        }

        return next(context);
    }
}
