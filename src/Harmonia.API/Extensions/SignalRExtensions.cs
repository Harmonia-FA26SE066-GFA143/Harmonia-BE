using System.Text.Json.Serialization;
using Harmonia.API.Hubs;
using Harmonia.API.Services;
using Harmonia.Application.Interfaces.IServices;

namespace Harmonia.API.Extensions;

public static class SignalRExtensions
{
    public static IServiceCollection AddApiSignalR(this IServiceCollection services)
    {
        // Same enum-as-name format as the controllers, so a pushed notification matches the list endpoint.
        services.AddSignalR()
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddScoped<INotificationPublisher, SignalRNotificationPublisher>();

        return services;
    }

    public static WebApplication MapApiHubs(this WebApplication app)
    {
        app.MapHub<NotificationHub>(NotificationHub.Route);

        return app;
    }
}
