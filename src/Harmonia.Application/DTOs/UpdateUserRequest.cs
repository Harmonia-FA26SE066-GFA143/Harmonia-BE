namespace Harmonia.Application.DTOs;

public class UpdateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Phone { get; set; }
}