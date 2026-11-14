using System.Threading.RateLimiting;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.API.Extensions;

public static class RateLimitingExtensions
{
    /// <summary>The anonymous join form on the home page: 5 submissions per IP every 10 minutes.</summary>
    public const string MemberRegistrationPolicy = "member-registration";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(MemberRegistrationPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10) }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse(ErrorCodes.RateLimitExceeded, "Too many requests, try again later"),
                    cancellationToken);
            };
        });

    public static WebApplication UseApiRateLimiting(this WebApplication app)
    {
        app.UseRateLimiter();
        return app;
    }
}
