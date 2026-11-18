namespace Harmonia.Infrastructure.ExternalServices;

/// <summary>Bound from the "Google" configuration section (see src/Harmonia.API/.env.example).</summary>
public class GoogleAuthOptions
{
    public const string SectionName = "Google";

    /// <summary>OAuth client ids (web, Android, iOS), comma separated; a token must be issued for one of them.</summary>
    public string ClientIds { get; set; } = string.Empty;

    public string[] GetClientIds() =>
        ClientIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
