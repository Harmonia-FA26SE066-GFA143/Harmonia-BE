using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

/// <summary>
/// Vietnamese wording of an English name the Liturgical Calendar API returns: a celebration ("Christ the King"), a
/// rank ("Solemnity") or a season ("OrdinaryTime"). The Admin keeps the list; celebrations that follow a pattern
/// (weekdays, numbered Sundays) are worded by code and need no row.
/// </summary>
public class LiturgicalNameTranslation : BaseAuditableEntity
{
    public LiturgicalNameKind Kind { get; set; }

    /// <summary>Exactly as stored on LiturgicalDay; unique within its <see cref="Kind"/>.</summary>
    public string EnglishName { get; set; } = string.Empty;

    public string VietnameseName { get; set; } = string.Empty;
}
