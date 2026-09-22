namespace Harmonia.Infrastructure.ExternalServices;

/// <summary>Bound from the "Jwt" configuration section (see src/Harmonia.API/.env.example).</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; }

    public int RefreshTokenExpiryDays { get; set; }
}
