namespace Harmonia.Infrastructure.ExternalServices;

/// <summary>Bound from the "Cloudinary" configuration section (see src/Harmonia.API/.env.example).</summary>
public class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    public int SignedUrlExpiryMinutes { get; set; }
}
