using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// The Admin's list of Vietnamese names for the English words of the Liturgical Calendar API, read whenever a
/// liturgical day is returned.
/// </summary>
public interface ILiturgicalNameTranslationService
{
    Task<Result<PagedList<LiturgicalNameTranslationDto>>> SearchAsync(
        SearchLiturgicalNameTranslationsRequest request, CancellationToken cancellationToken);

    /// <summary>LITURGICAL_NAME_TRANSLATION_DUPLICATE when the kind and English name already have a row.</summary>
    Task<Result<LiturgicalNameTranslationDto>> CreateAsync(
        SaveLiturgicalNameTranslationRequest request, CancellationToken cancellationToken);

    /// <summary>LITURGICAL_NAME_TRANSLATION_NOT_FOUND, LITURGICAL_NAME_TRANSLATION_DUPLICATE.</summary>
    Task<Result<LiturgicalNameTranslationDto>> UpdateAsync(
        Guid id, SaveLiturgicalNameTranslationRequest request, CancellationToken cancellationToken);

    /// <summary>Hard delete: the name falls back to a pattern or to English. LITURGICAL_NAME_TRANSLATION_NOT_FOUND.</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Celebration, rank and season names of the synced days that neither a row nor a pattern translates, so the
    /// Admin sees what still shows in English. Ordered by kind, then name.
    /// </summary>
    Task<Result<List<UntranslatedLiturgicalNameResponse>>> GetMissingAsync(CancellationToken cancellationToken);
}
