namespace Harmonia.Application.Common.Models;

/// <summary>Where the emailed reset link points; the raw token is appended as <c>?token=</c>.</summary>
public class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    public string WebUrl { get; set; } = string.Empty;

    public string MobileUrl { get; set; } = string.Empty;
}
