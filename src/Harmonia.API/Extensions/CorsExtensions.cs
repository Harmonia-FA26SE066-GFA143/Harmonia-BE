namespace Harmonia.API.Extensions;

public static class CorsExtensions
{
    private const string PolicyName = "HarmoniaCors";

    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = (configuration["Cors:AllowedOrigins"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        services.AddCors(options =>
        {
            // Named origins only. AllowAnyOrigin() + AllowCredentials() is a runtime error in
            // ASP.NET Core, and credentials must stay allowed for the future NotificationHub.
            options.AddPolicy(PolicyName, policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    public static IApplicationBuilder UseApiCors(this IApplicationBuilder app) => app.UseCors(PolicyName);
}
