namespace Harmonia.Infrastructure.ExternalServices;

/// <summary>Bound from the "Brevo" configuration section (see src/Harmonia.API/.env.example).</summary>
public class BrevoOptions
{
    public const string SectionName = "Brevo";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Must be a verified sender in Brevo (Senders), otherwise every send is rejected.</summary>
    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;

    /// <summary>Public folder (Cloudinary) holding the email images used by <see cref="EmailLayout"/>.</summary>
    public string AssetBaseUrl { get; set; } = string.Empty;
}
