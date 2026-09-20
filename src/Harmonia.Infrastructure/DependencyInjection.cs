using Harmonia.Infrastructure.Data;
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

        // Pin the version instead of ServerVersion.AutoDetect: AutoDetect opens a real
        // connection at startup, which breaks `dotnet ef migrations add` when MySQL is down.
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

        services.AddDbContext<HarmoniaDbContext>(options =>
            options.UseMySql(connectionString, serverVersion));

        return services;
    }
}
