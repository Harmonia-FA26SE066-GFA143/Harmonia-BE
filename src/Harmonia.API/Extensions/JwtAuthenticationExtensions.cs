using System.Text;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Harmonia.API.Extensions;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddApiJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var key = jwtSection["Key"]
            ?? throw new InvalidOperationException("Missing Jwt__Key. See src/Harmonia.API/.env.example.");
        var issuer = jwtSection["Issuer"]
            ?? throw new InvalidOperationException("Missing Jwt__Issuer. See src/Harmonia.API/.env.example.");
        var audience = jwtSection["Audience"]
            ?? throw new InvalidOperationException("Missing Jwt__Audience. See src/Harmonia.API/.env.example.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ClockSkew = TimeSpan.Zero,
                };

                options.Events = new JwtBearerEvents
                {
                    // A browser cannot set an Authorization header on a WebSocket handshake, so the
                    // hub paths - and only those - also accept the token as a query parameter.
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/hubs"))
                        {
                            var accessToken = context.Request.Query["access_token"];
                            if (!string.IsNullOrEmpty(accessToken))
                            {
                                context.Token = accessToken;
                            }
                        }

                        return Task.CompletedTask;
                    },

                    // Default JwtBearer challenge doesn't match doc/error-codes.md's {code,message} contract.
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";

                        var code = context.AuthenticateFailure is SecurityTokenExpiredException
                            ? ErrorCodes.AuthTokenExpired
                            : ErrorCodes.AuthTokenInvalid;

                        await context.Response.WriteAsJsonAsync(new ErrorResponse(code, "Authentication failed"));
                    },
                };
            });

        services.AddAuthorization();

        return services;
    }
}
