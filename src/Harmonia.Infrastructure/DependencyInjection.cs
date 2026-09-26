using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Infrastructure.Data;
using Harmonia.Infrastructure.ExternalServices;
using Harmonia.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Harmonia.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Missing ConnectionStrings__DefaultConnection. See src/Harmonia.API/.env.example.");

        services.AddDbContext<HarmoniaDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Key)
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

        services.AddHttpContextAccessor();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasherService, PasswordHasherService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IFileStorageService, CloudinaryFileStorageService>();

        return services;
    }
}
