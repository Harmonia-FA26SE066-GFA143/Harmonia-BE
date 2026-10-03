using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>Body of PUT api/member-profiles/{id}: what the Choir Director manages on top of the member's own fields.</summary>
public class UpdateMemberProfileRequest
{
    public string? Phone { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public DateOnly JoinedDate { get; set; }

    public MemberStatus Status { get; set; }
}
