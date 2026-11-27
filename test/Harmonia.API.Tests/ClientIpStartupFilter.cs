using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Harmonia.API.Tests;

/// <summary>
/// TestServer leaves RemoteIpAddress null, so every test would share one rate-limit bucket and sign-ins would hit 429.
/// Each request gets a fresh IP unless it sends <see cref="Header"/>, which rate-limit tests use to stay in one bucket.
/// </summary>
public sealed class ClientIpStartupFilter : IStartupFilter
{
    public const string Header = "X-Test-Client-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.TryParse(context.Request.Headers[Header], out var ip)
                ? ip
                : new IPAddress(Random.Shared.NextInt64(1, uint.MaxValue));
            return nextMiddleware(context);
        });
        next(app);
    };
}
