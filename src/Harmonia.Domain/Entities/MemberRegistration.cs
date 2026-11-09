using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

/// <summary>
/// A request to join the choir, sent from the public home page without signing in. The Admin turns it into a
/// Choir Member account or deletes it; either way the row is removed, so every row is still waiting.
/// </summary>
public class MemberRegistration : BaseAuditableEntity
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    /// <summary>Free text from the applicant, such as the choir they would like to join.</summary>
    public string? Note { get; set; }
}
