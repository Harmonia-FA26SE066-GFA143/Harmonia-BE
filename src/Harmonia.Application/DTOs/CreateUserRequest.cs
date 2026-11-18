using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? SaintName { get; set; }
    public Gender? Gender { get; set; }
    public string RoleName { get; set; } = string.Empty; // "Admin" | "ParishPriest" | "ChoirDirector" | "ChoirMember"

    /// <summary>ChoirMember only: the new member's join request goes to this choir's director (D9).</summary>
    public Guid? PreferredChoirId { get; set; }
}