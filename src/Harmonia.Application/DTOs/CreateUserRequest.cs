namespace Harmonia.Application.DTOs;

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty; // "Admin" | "ParishPriest" | "ChoirDirector" | "ChoirMember"
}