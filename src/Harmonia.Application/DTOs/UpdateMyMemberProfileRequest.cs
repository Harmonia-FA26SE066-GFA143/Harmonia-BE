namespace Harmonia.Application.DTOs;

/// <summary>Body of PUT api/member-profiles/me. FullName is stored on the member's own User.</summary>
public class UpdateMyMemberProfileRequest
{
    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public DateOnly? DateOfBirth { get; set; }
}
