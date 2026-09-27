using Harmonia.Application.Common.Models;
using Harmonia.Infrastructure.ExternalServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Harmonia.Infrastructure.Tests;

/// <summary>03-security.md: a missing secret must fail at startup with the key name, not as a null later.</summary>
public class DependencyInjectionTests
{
    private static readonly Dictionary<string, string?> ValidSettings = new()
    {
        ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused",
        ["Jwt:Key"] = "test-signing-key-that-is-at-least-32-bytes-long!!",
        ["Jwt:Issuer"] = "issuer",
        ["Jwt:Audience"] = "audience",
        ["Jwt:ExpiryMinutes"] = "30",
        ["Jwt:RefreshTokenExpiryDays"] = "7",
        ["Cloudinary:CloudName"] = "cloud",
        ["Cloudinary:ApiKey"] = "key",
        ["Cloudinary:ApiSecret"] = "secret",
        ["Cloudinary:SignedUrlExpiryMinutes"] = "10",
        ["Brevo:ApiKey"] = "key",
        ["Brevo:FromEmail"] = "noreply@test.com",
        ["Brevo:FromName"] = "Harmonia",
        ["PasswordReset:WebUrl"] = "https://web.test/reset",
        ["PasswordReset:MobileUrl"] = "harmonia://reset",
    };

    private static ServiceProvider Build(Action<Dictionary<string, string?>>? change = null)
    {
        var settings = new Dictionary<string, string?>(ValidSettings);
        change?.Invoke(settings);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddLogging().AddInfrastructure(configuration).BuildServiceProvider();
    }

    [Fact]
    public void ValidSettings_AllOptionsResolve()
    {
        using var provider = Build();

        Assert.Equal("issuer", provider.GetRequiredService<IOptions<JwtOptions>>().Value.Issuer);
        Assert.Equal("Harmonia", provider.GetRequiredService<IOptions<BrevoOptions>>().Value.FromName);
        Assert.Equal("cloud", provider.GetRequiredService<IOptions<CloudinaryOptions>>().Value.CloudName);
        Assert.Equal("harmonia://reset", provider.GetRequiredService<IOptions<PasswordResetOptions>>().Value.MobileUrl);
    }

    [Fact]
    public void MissingConnectionString_ThrowsWithKeyName()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Build(s => s.Remove("ConnectionStrings:DefaultConnection")));

        Assert.Contains("ConnectionStrings__DefaultConnection", ex.Message);
    }

    [Theory]
    [InlineData("Jwt:Key")]
    [InlineData("Jwt:Issuer")]
    [InlineData("Jwt:Audience")]
    [InlineData("Jwt:ExpiryMinutes")]
    [InlineData("Jwt:RefreshTokenExpiryDays")]
    public void MissingJwtSetting_FailsValidationWithSectionName(string key)
    {
        using var provider = Build(s => s.Remove(key));

        var ex = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<JwtOptions>>().Value);
        Assert.Contains("Jwt__", ex.Message);
    }

    [Theory]
    [InlineData("short-key")]
    [InlineData("0123456789abcdef0123456789abcde")] // 31 bytes
    [InlineData("                                ")] // 32 bytes, all whitespace
    public void JwtKeyUnder256BitsOrBlank_FailsValidation(string key)
    {
        using var provider = Build(s => s["Jwt:Key"] = key);

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<JwtOptions>>().Value);
    }

    [Fact]
    public void JwtKeyOfExactly256Bits_Passes()
    {
        using var provider = Build(s => s["Jwt:Key"] = "0123456789abcdef0123456789abcdef");

        Assert.Equal(32, provider.GetRequiredService<IOptions<JwtOptions>>().Value.Key.Length);
    }

    [Theory]
    [InlineData("Brevo:ApiKey")]
    [InlineData("Brevo:FromEmail")]
    public void MissingBrevoSetting_FailsValidation(string key)
    {
        using var provider = Build(s => s.Remove(key));

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<BrevoOptions>>().Value);
    }

    [Theory]
    [InlineData("Cloudinary:ApiSecret")]
    [InlineData("Cloudinary:SignedUrlExpiryMinutes")]
    public void MissingCloudinarySetting_FailsValidation(string key)
    {
        using var provider = Build(s => s.Remove(key));

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CloudinaryOptions>>().Value);
    }

    [Theory]
    [InlineData("PasswordReset:WebUrl", null)]
    [InlineData("PasswordReset:WebUrl", "not a url")]
    [InlineData("PasswordReset:MobileUrl", "/relative")]
    public void InvalidPasswordResetUrl_FailsValidation(string key, string? value)
    {
        using var provider = Build(s => s[key] = value);

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<PasswordResetOptions>>().Value);
    }
}
