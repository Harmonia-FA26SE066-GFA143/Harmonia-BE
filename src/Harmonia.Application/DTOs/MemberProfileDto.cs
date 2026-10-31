using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class MemberProfileDto
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public DateOnly JoinedDate { get; set; }

    public MemberStatus Status { get; set; }

    public string? AvatarUrl { get; set; }
}
