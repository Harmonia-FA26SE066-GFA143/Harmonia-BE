using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? DeviceId { get; set; }

    public DevicePlatform? Platform { get; set; }
}
