using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;

    /// <summary>Picks the reset link: Android/iOS get the mobile link, Web or null gets the web link.</summary>
    public DevicePlatform? Platform { get; set; }
}
