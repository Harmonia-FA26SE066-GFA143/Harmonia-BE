using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Active rows of the Admin-configured catalogs, for dropdowns. Inactive rows are hidden.</summary>
public interface ILookupService
{
    Task<Result<List<LookupDto>>> GetMassTypesAsync(CancellationToken cancellationToken);

    Task<Result<List<LookupDto>>> GetCeremonyTypesAsync(CancellationToken cancellationToken);

    Task<Result<List<LookupDto>>> GetEventCategoriesAsync(CancellationToken cancellationToken);

    Task<Result<List<LookupDto>>> GetSongThemesAsync(CancellationToken cancellationToken);

    Task<Result<List<LookupDto>>> GetSkillCategoriesAsync(CancellationToken cancellationToken);

    /// <summary>Ordered by start date.</summary>
    Task<Result<List<LiturgicalSeasonDto>>> GetLiturgicalSeasonsAsync(CancellationToken cancellationToken);

    /// <summary>Ordered by DefaultOrder, the position of the slot in a Mass.</summary>
    Task<Result<List<LiturgicalSlotDto>>> GetLiturgicalSlotsAsync(CancellationToken cancellationToken);

    Task<Result<List<WorshipLocationDto>>> GetWorshipLocationsAsync(CancellationToken cancellationToken);

    /// <summary>Active skills of active categories; <paramref name="categoryId"/> narrows to one category.</summary>
    Task<Result<List<SkillDto>>> GetSkillsAsync(Guid? categoryId, CancellationToken cancellationToken);
}
