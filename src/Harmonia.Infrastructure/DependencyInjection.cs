using System.Text;
using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Infrastructure.Data;
using Harmonia.Infrastructure.Data.Interceptors;
using Harmonia.Infrastructure.ExternalServices;
using Harmonia.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Harmonia.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Missing ConnectionStrings__DefaultConnection. See src/Harmonia.API/.env.example.");

        // Scoped: it reads the caller through ICurrentUserService, which is per request.
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddDbContext<HarmoniaDbContext>((serviceProvider, options) =>
            options.UseSqlServer(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                // HMAC-SHA256 signs with the UTF-8 bytes of Key: 03-security.md requires at least 256 bits.
                o => Encoding.UTF8.GetByteCount(o.Key) >= 32
                    && !string.IsNullOrWhiteSpace(o.Key)
                    && !string.IsNullOrWhiteSpace(o.Issuer)
                    && !string.IsNullOrWhiteSpace(o.Audience)
                    && o.ExpiryMinutes > 0
                    && o.RefreshTokenExpiryDays > 0,
                "Missing or invalid Jwt__* configuration. See src/Harmonia.API/.env.example.")
            .ValidateOnStart();

        services.AddOptions<CloudinaryOptions>()
            .Bind(configuration.GetSection(CloudinaryOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.CloudName)
                    && !string.IsNullOrWhiteSpace(o.ApiKey)
                    && !string.IsNullOrWhiteSpace(o.ApiSecret)
                    && o.SignedUrlExpiryMinutes > 0,
                "Missing or invalid Cloudinary__* configuration. See src/Harmonia.API/.env.example.")
            .ValidateOnStart();

        services.AddOptions<BrevoOptions>()
            .Bind(configuration.GetSection(BrevoOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.ApiKey)
                    && !string.IsNullOrWhiteSpace(o.FromEmail)
                    && !string.IsNullOrWhiteSpace(o.FromName),
                "Missing or invalid Brevo__* configuration. See src/Harmonia.API/.env.example.")
            .ValidateOnStart();

        // Read by AuthService in Application, bound here because this is where IConfiguration is available.
        services.AddOptions<PasswordResetOptions>()
            .Bind(configuration.GetSection(PasswordResetOptions.SectionName))
            .Validate(
                o => Uri.IsWellFormedUriString(o.WebUrl, UriKind.Absolute)
                    && Uri.IsWellFormedUriString(o.MobileUrl, UriKind.Absolute),
                "Missing or invalid PasswordReset__* configuration. See src/Harmonia.API/.env.example.")
            .ValidateOnStart();

        services.AddOptions<GoogleAuthOptions>()
            .Bind(configuration.GetSection(GoogleAuthOptions.SectionName))
            .Validate(
                o => o.GetClientIds().Length > 0,
                "Missing or invalid Google__* configuration. See src/Harmonia.API/.env.example.")
            .ValidateOnStart();

        services.AddHttpContextAccessor();

        // Plain CRUD: inject IGenericRepository<T> directly, no per-entity repository needed.
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IMemberProfileRepository, MemberProfileRepository>();
        services.AddScoped<ISongRepository, SongRepository>();
        services.AddScoped<IMusicMaterialRepository, MusicMaterialRepository>();
        services.AddScoped<IMaterialLearningProgressRepository, MaterialLearningProgressRepository>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasherService, PasswordHasherService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IFileStorageService, CloudinaryFileStorageService>();
        services.AddSingleton<IEmailSender, BrevoEmailSender>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<DataSeeder>();

        return services;
    }

    /// <summary>Inserts the four roles and the first Admin when missing. Never updates or deletes.</summary>
    public static async Task SeedDataAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        try
        {
            await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best effort: a database that is briefly unreachable (e.g. Azure SQL resuming) must not
            // stop the app from starting; deployed databases already hold the seed anyway.
            scope.ServiceProvider.GetRequiredService<ILogger<DataSeeder>>()
                .LogError(ex, "Data seeding failed; the app keeps starting without it.");
        }
    }
}
