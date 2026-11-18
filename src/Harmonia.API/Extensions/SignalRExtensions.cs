using System.Text.Json.Serialization;
using Harmonia.API.Hubs;
using Harmonia.API.Services;
using Harmonia.Application.Interfaces.IServices;

namespace Harmonia.API.Extensions;

public static class SignalRExtensions
{
    private const string AzureConnectionStringKey = "Azure:SignalR:ConnectionString";

    public static IServiceCollection AddApiSignalR(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // Same enum-as-name format as the controllers, so a pushed notification matches the list endpoint.
        var signalR = services.AddSignalR()
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // Azure SignalR Service (Default mode) holds the client connections, so pushes reach users on every
        // App Service instance. Development and Testing may run in-process instead; anywhere else the key is required.
        var connectionString = configuration[AzureConnectionStringKey];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            signalR.AddAzureSignalR(connectionString);
        }
        else if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException("Missing configuration: Azure__SignalR__ConnectionString");
        }

        services.AddScoped<INotificationPublisher, SignalRNotificationPublisher>();

        return services;
    }

    public static WebApplication MapApiHubs(this WebApplication app)
    {
        app.MapHub<NotificationHub>(NotificationHub.Route);

        return app;
    }
}
