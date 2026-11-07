using Harmonia.Application.Common.Models;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/liturgical-name-translations.</summary>
public class SearchLiturgicalNameTranslationsRequest : PagingRequest
{
    /// <summary>Matches either name, ignoring case and diacritics.</summary>
    public string? Keyword { get; set; }

    public LiturgicalNameKind? Kind { get; set; }
}
