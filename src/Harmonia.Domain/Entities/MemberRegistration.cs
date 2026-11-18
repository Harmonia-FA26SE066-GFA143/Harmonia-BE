using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

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

    public string? SaintName { get; set; }

    public Gender? Gender { get; set; }

    /// <summary>Free text from the applicant.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// The choir the applicant would like to join; null = no preference. Creating the account turns it into a join
    /// request to that choir's director (D9).
    /// </summary>
    public Guid? PreferredChoirId { get; set; }

    public Choir? PreferredChoir { get; set; }
}
