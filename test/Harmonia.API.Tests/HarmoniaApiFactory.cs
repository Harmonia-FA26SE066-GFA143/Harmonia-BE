using System.Net.Http.Headers;
using System.Net.Http.Json;
using Harmonia.API.Controllers;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Harmonia.Infrastructure.Data.Interceptors;
using Harmonia.Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Harmonia.API.Tests;

/// <summary>
/// The real API pipeline over SQLite in-memory, with email replaced by a substitute.
/// One instance per test class (IClassFixture), so each class starts from the same seed.
/// </summary>
public sealed class HarmoniaApiFactory : WebApplicationFactory<AuthController>
{
    public const string Password = "Passw0rd!";
    public const string AllowedOrigin = "https://web.harmonia.test";

    public static readonly string[] SeededEmails =
    [
        "admin@test.com", "priest@test.com", "director@test.com", "member@test.com", "member2@test.com",
    ];

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    // Empty content root: Program.cs loads <ContentRoot>/.env, and the developer's real .env
    // (real database, real Brevo key) must never be picked up by tests.
    private readonly string _contentRoot = Directory.CreateTempSubdirectory("harmonia-api-tests").FullName;

    public IEmailSender EmailSender { get; } = Substitute.For<IEmailSender>();

    public HarmoniaApiFactory()
    {
        _connection.Open();

        // The schema must exist before Program.cs runs DataSeeder at startup.
        using (var context = new HarmoniaDbContext(
            new DbContextOptionsBuilder<HarmoniaDbContext>().UseSqlite(_connection).Options))
        {
            context.Database.EnsureCreated();
        }

        EmailSender.SendAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(_contentRoot);

        // UseSetting (not ConfigureAppConfiguration): Program.cs reads these before Build().
        builder.UseSetting("ConnectionStrings:DefaultConnection", "DataSource=:memory:");
        builder.UseSetting("Jwt:Key", "test-signing-key-that-is-at-least-32-bytes-long!!");
        builder.UseSetting("Jwt:Issuer", "harmonia-test");
        builder.UseSetting("Jwt:Audience", "harmonia-test-clients");
        builder.UseSetting("Jwt:ExpiryMinutes", "30");
        builder.UseSetting("Jwt:RefreshTokenExpiryDays", "7");
        builder.UseSetting("Cors:AllowedOrigins", AllowedOrigin);
        builder.UseSetting("Cloudinary:CloudName", "test");
        builder.UseSetting("Cloudinary:ApiKey", "test");
        builder.UseSetting("Cloudinary:ApiSecret", "test");
        builder.UseSetting("Cloudinary:SignedUrlExpiryMinutes", "10");
        builder.UseSetting("Brevo:ApiKey", "test");
        builder.UseSetting("Brevo:FromEmail", "noreply@test.com");
        builder.UseSetting("Brevo:FromName", "Harmonia Test");
        builder.UseSetting("PasswordReset:WebUrl", "https://web.harmonia.test/reset");
        builder.UseSetting("PasswordReset:MobileUrl", "harmonia://reset");
        builder.UseSetting("Google:ClientIds", "test-client-id.apps.googleusercontent.com");

        builder.ConfigureTestServices(services =>
        {
            // Drop every SQL Server registration, including EF 9's accumulated option configurations.
            Remove(services, typeof(DbContextOptions<HarmoniaDbContext>), typeof(DbContextOptions),
                typeof(IDbContextOptionsConfiguration<HarmoniaDbContext>));
            services.AddDbContext<HarmoniaDbContext>((sp, options) =>
                options.UseSqlite(_connection).AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

            Remove(services, typeof(IEmailSender));
            services.AddSingleton(EmailSender);
        });
    }

    private static void Remove(IServiceCollection services, params Type[] serviceTypes)
    {
        foreach (var descriptor in services.Where(d => serviceTypes.Contains(d.ServiceType)).ToList())
        {
            services.Remove(descriptor);
        }
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        Seed(host.Services);
        return host;
    }

    private static void Seed(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HarmoniaDbContext>();

        // Roles come from the real DataSeeder, which already ran at startup.
        var roles = context.Roles.ToDictionary(r => r.Name);

        var hasher = new PasswordHasherService();
        string[] roleOfEmail = [RoleNames.Admin, RoleNames.ParishPriest, RoleNames.ChoirDirector, RoleNames.ChoirMember, RoleNames.ChoirMember];
        for (var i = 0; i < SeededEmails.Length; i++)
        {
            var user = new User { Id = Guid.NewGuid(), Email = SeededEmails[i], RoleId = roles[roleOfEmail[i]].Id };
            user.PasswordHash = hasher.HashPassword(user, Password);
            context.Users.Add(user);
        }

        context.SaveChanges();
    }

    /// <summary>Adds an extra active user, for tests that change a password and must not disturb the seed.</summary>
    public async Task<User> AddUserAsync(
        string email,
        string roleName = RoleNames.ChoirMember,
        bool isActive = true,
        CancellationToken cancellationToken = default)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HarmoniaDbContext>();
        var role = await context.Roles.SingleAsync(r => r.Name == roleName, cancellationToken);
        var user = new User { Id = Guid.NewGuid(), Email = email, RoleId = role.Id, IsActive = isActive };
        user.PasswordHash = new PasswordHasherService().HashPassword(user, Password);
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<T> WithDbAsync<T>(
        Func<HarmoniaDbContext, CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<HarmoniaDbContext>(), cancellationToken);
    }

    public async Task<LoginResponse> LoginAsync(
        string email, string password = Password, CancellationToken cancellationToken = default)
    {
        var response = await CreateClient().PostAsJsonAsync(
            "api/auth/login", new { email, password }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options, cancellationToken))!;
    }

    public async Task<HttpClient> CreateClientAsAsync(string email, CancellationToken cancellationToken = default)
    {
        var login = await LoginAsync(email, cancellationToken: cancellationToken);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
            try { Directory.Delete(_contentRoot, recursive: true); } catch (IOException) { }
        }
    }
}
