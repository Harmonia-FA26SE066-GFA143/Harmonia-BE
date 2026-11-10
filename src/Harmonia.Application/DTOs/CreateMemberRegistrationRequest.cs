namespace Harmonia.Application.DTOs;

/// <summary>Body of the anonymous POST api/member-registrations sent from the public home page.</summary>
public class CreateMemberRegistrationRequest
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? SaintName { get; set; }

    public string? Note { get; set; }
}
