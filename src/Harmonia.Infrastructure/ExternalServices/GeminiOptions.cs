namespace Harmonia.Infrastructure.ExternalServices;

/// <summary>Bound from the "Gemini" configuration section (see src/Harmonia.API/.env.example).</summary>
public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model id in the generateContent URL, e.g. "gemini-2.5-flash".</summary>
    public string Model { get; set; } = string.Empty;
}
