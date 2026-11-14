namespace Harmonia.Application.DTOs;

/// <summary>An active member of a choir, from <c>ChoirMembership</c> and its <c>MemberProfile</c>.</summary>
public class ChoirMemberDto
{
    public Guid MemberId { get; set; }

    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateOnly JoinedDate { get; set; }
}
