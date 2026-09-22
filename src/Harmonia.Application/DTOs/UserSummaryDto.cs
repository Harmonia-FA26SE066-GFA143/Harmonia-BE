namespace Harmonia.Application.DTOs;

public class UserSummaryDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;
}
