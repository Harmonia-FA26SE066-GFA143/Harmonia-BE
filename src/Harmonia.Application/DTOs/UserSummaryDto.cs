namespace Harmonia.Application.DTOs;

public class UserSummaryDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    /// <summary>True until the user replaces the password the Admin emailed; the client sends them to change it.</summary>
    public bool IsPasswordChangeRequired { get; set; }
}
