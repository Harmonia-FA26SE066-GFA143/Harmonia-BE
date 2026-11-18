namespace Harmonia.Application.DTOs;

/// <summary>Another waiting registration or an existing account that may belong to the same person.</summary>
public class RegistrationDuplicateDto
{
    /// <summary>"Registration" (another waiting row) or "User" (an existing account).</summary>
    public string Kind { get; set; } = string.Empty;

    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>"Phone" (same number) or "Name" (same full name and saint name, ignoring case and accents).</summary>
    public string MatchedOn { get; set; } = string.Empty;
}
