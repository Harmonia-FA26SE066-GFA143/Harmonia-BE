namespace Harmonia.Application.DTOs;

/// <summary>Body of PUT api/auth/me: any signed-in user edits their own name and phone.</summary>
public class UpdateMyUserRequest
{
    public string? FullName { get; set; }

    public string? Phone { get; set; }
}
