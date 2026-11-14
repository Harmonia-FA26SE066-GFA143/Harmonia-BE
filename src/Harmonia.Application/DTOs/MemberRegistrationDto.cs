using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class MemberRegistrationDto
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? SaintName { get; set; }

    public Gender? Gender { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
}
