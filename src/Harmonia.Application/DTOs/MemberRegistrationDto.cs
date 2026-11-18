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

    public Guid? PreferredChoirId { get; set; }

    public string? PreferredChoirName { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Rows that may be the same person under another email; a hint for the Admin, never a block.</summary>
    public List<RegistrationDuplicateDto> PossibleDuplicates { get; set; } = [];
}
