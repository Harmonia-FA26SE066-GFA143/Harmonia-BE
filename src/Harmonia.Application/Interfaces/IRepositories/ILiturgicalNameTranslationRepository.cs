using Harmonia.Application.Common.Models;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface ILiturgicalNameTranslationRepository : IGenericRepository<LiturgicalNameTranslation>
{
    /// <summary>Read-only page ordered by kind, then English name; <paramref name="keyword"/> matches either name.</summary>
    Task<PagedList<LiturgicalNameTranslation>> SearchAsync(
        string? keyword, LiturgicalNameKind? kind, PagingRequest paging, CancellationToken cancellationToken);

    /// <summary>True when another row of the same kind already translates <paramref name="englishName"/>.</summary>
    Task<bool> ExistsAsync(
        LiturgicalNameKind kind, string englishName, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>Every translation keyed by kind and English name; names compare exactly as stored.</summary>
    Task<Dictionary<(LiturgicalNameKind Kind, string EnglishName), string>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Distinct celebration, rank and season names of the cached liturgical days.</summary>
    Task<List<(LiturgicalNameKind Kind, string EnglishName)>> GetCalendarNamesAsync(CancellationToken cancellationToken);
}
