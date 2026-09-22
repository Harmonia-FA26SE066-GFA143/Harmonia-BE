using Harmonia.API.Hubs;
using Harmonia.API.Services;
using Harmonia.Application.Interfaces.IServices;

namespace Harmonia.API.Extensions;

public static class SignalRExtensions
{
    public static IServiceCollection AddApiSignalR(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddScoped<INotificationPublisher, SignalRNotificationPublisher>();

        return services;
    }

    public static WebApplication MapApiHubs(this WebApplication app)
    {
        app.MapHub<NotificationHub>(NotificationHub.Route);

        return app;
    }
}
