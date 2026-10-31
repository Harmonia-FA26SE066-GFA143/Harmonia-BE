using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>Create / update body of a liturgical name translation. Both names are trimmed.</summary>
public class SaveLiturgicalNameTranslationRequest
{
    public LiturgicalNameKind Kind { get; set; }

    /// <summary>Exactly as the calendar returns it, e.g. "Christ the King", "Solemnity", "OrdinaryTime".</summary>
    public string EnglishName { get; set; } = string.Empty;

    public string VietnameseName { get; set; } = string.Empty;
}
