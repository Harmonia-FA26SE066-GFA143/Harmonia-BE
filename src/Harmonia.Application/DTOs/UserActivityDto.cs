namespace Harmonia.Application.DTOs;

/// <summary>One account with its sensitive activity inside the report's date range (UC-34 / FE-52).</summary>
public class UserActivityDto
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    /// <summary>Current value, not limited to the date range.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Times the user changed their own password while signed in.</summary>
    public int PasswordChangeCount { get; set; }

    /// <summary>Times the password was reset through the emailed link.</summary>
    public int PasswordResetCount { get; set; }

    /// <summary>Times the account itself was edited: role, activation, name, phone or email.</summary>
    public int AccountChangeCount { get; set; }

    /// <summary>Audit log entries this user caused anywhere in the system.</summary>
    public int ActionCount { get; set; }
}
