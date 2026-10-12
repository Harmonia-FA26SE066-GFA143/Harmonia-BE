using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Admin-configured catalogs. Reads return active rows for dropdowns; <c>includeInactive</c> also returns the
/// switched-off rows for the Admin catalog pages. Writes create or update (id null = create) and never delete
/// (UC-32 / FE-49, FE-50).
/// </summary>
public interface ILookupService
{
    Task<Result<List<MassTypeDto>>> GetMassTypesAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<Result<List<CeremonyTypeDto>>> GetCeremonyTypesAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<Result<List<EventCategoryDto>>> GetEventCategoriesAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<Result<List<SongThemeDto>>> GetSongThemesAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<Result<List<SkillCategoryDto>>> GetSkillCategoriesAsync(bool includeInactive, CancellationToken cancellationToken);

    /// <summary>Ordered by start date.</summary>
    Task<Result<List<LiturgicalSeasonDto>>> GetLiturgicalSeasonsAsync(bool includeInactive, CancellationToken cancellationToken);

    /// <summary>Ordered by DefaultOrder, the position of the slot in a Mass.</summary>
    Task<Result<List<LiturgicalSlotDto>>> GetLiturgicalSlotsAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<Result<List<WorshipLocationDto>>> GetWorshipLocationsAsync(bool includeInactive, CancellationToken cancellationToken);

    /// <summary>
    /// Active skills of active categories, or every skill with <paramref name="includeInactive"/>;
    /// <paramref name="categoryId"/> narrows to one category.
    /// </summary>
    Task<Result<List<SkillDto>>> GetSkillsAsync(Guid? categoryId, bool includeInactive, CancellationToken cancellationToken);

    Task<Result<MassTypeDto>> SaveMassTypeAsync(Guid? id, SaveCatalogItemRequest request, CancellationToken cancellationToken);

    Task<Result<CeremonyTypeDto>> SaveCeremonyTypeAsync(Guid? id, SaveCatalogItemRequest request, CancellationToken cancellationToken);

    Task<Result<EventCategoryDto>> SaveEventCategoryAsync(Guid? id, SaveCatalogItemRequest request, CancellationToken cancellationToken);

    Task<Result<SkillCategoryDto>> SaveSkillCategoryAsync(Guid? id, SaveCatalogItemRequest request, CancellationToken cancellationToken);

    /// <summary>The category must exist; the name is unique within the category.</summary>
    Task<Result<SkillDto>> SaveSkillAsync(Guid? id, SaveSkillRequest request, CancellationToken cancellationToken);

    /// <summary>An active season must not overlap the dates of another active season.</summary>
    Task<Result<LiturgicalSeasonDto>> SaveLiturgicalSeasonAsync(
        Guid? id, SaveLiturgicalSeasonRequest request, CancellationToken cancellationToken);

    Task<Result<SongThemeDto>> SaveSongThemeAsync(Guid? id, SaveCatalogItemRequest request, CancellationToken cancellationToken);

    Task<Result<LiturgicalSlotDto>> SaveLiturgicalSlotAsync(Guid? id, SaveLiturgicalSlotRequest request, CancellationToken cancellationToken);

    Task<Result<WorshipLocationDto>> SaveWorshipLocationAsync(Guid? id, SaveWorshipLocationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the catalog rows an event is about to point at: LOOKUP_NOT_FOUND for an unknown id, LOOKUP_INACTIVE
    /// for a switched-off row, EVENT_DATE_OUTSIDE_SEASON when <paramref name="eventDate"/> is not inside the season.
    /// A row <paramref name="current"/> already points at may stay even when switched off, so an old event
    /// remains editable.
    /// </summary>
    Task<Result> CheckEventCatalogsAsync(
        DateOnly eventDate, Guid? liturgicalSeasonId, Guid? massTypeId, Guid? ceremonyTypeId, Guid? categoryId,
        Guid locationId, LiturgicalEvent? current, CancellationToken cancellationToken);
}
