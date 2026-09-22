namespace Harmonia.Application.DTOs;

/// <summary>Non-entity payload: the pair of tokens plus enough user info for the client to route by role.</summary>
public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public DateTime AccessTokenExpiresAt { get; set; }

    public string RefreshToken { get; set; } = string.Empty;

    public UserSummaryDto User { get; set; } = null!;
}
