using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class GoogleLoginRequest
{
    /// <summary>The ID token the client received from Google Sign-In.</summary>
    public string IdToken { get; set; } = string.Empty;

    public string? DeviceId { get; set; }

    public DevicePlatform? Platform { get; set; }
}
