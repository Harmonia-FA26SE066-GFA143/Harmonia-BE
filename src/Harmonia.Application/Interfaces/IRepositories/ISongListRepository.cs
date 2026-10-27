using Harmonia.Domain.Entities; 
using Harmonia.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface ISongListRepository : IGenericRepository<SongList>
{
    Task<SongList?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken);

    Task<SongList?> GetLatestVersionAsync(Guid eventId, CancellationToken cancellationToken);

    Task<SongList?> GetApprovedForEventAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>Newest version of the event with Items and Reviews loaded; Draft versions are skipped unless <paramref name="includeDraft"/>.</summary>
    Task<SongList?> GetLatestWithDetailsAsync(Guid eventId, bool includeDraft, CancellationToken cancellationToken);

    Task<List<SongList>> GetByStatusAsync(SongListStatus status, CancellationToken cancellationToken);

    Task<List<SongListItem>> GetItemsAsync(Guid songListId, CancellationToken cancellationToken);

    Task RemoveItemsAsync(IEnumerable<SongListItem> items, CancellationToken cancellationToken);

    Task AddItemsAsync(IEnumerable<SongListItem> items, CancellationToken cancellationToken);

    Task AddReviewAsync(SongListReview review, CancellationToken cancellationToken);

    /// <summary>Read-only items of the list with their PersonnelRequirements loaded.</summary>
    Task<List<SongListItem>> GetItemsWithRequirementsAsync(Guid songListId, CancellationToken cancellationToken);

    /// <summary>
    /// Mandatory library requirements (vocal and instrument tables) of the given songs, for active skills outside the
    /// Vocal category: choir parts are sung by everyone and never staffed per song.
    /// </summary>
    Task<List<(Guid SongId, Guid SkillId, int RequiredCount)>> GetSongDefaultRequirementsAsync(
        IReadOnlyCollection<Guid> songIds, CancellationToken cancellationToken);

    /// <summary>Requirements of the given liturgical slots, for active skills.</summary>
    Task<List<(Guid SlotId, Guid SkillId, int RequiredCount)>> GetSlotDefaultRequirementsAsync(
        IReadOnlyCollection<Guid> slotIds, CancellationToken cancellationToken);

    /// <summary>Skill → count the event already needs for the whole service (EventPersonnelRequirement).</summary>
    Task<Dictionary<Guid, int>> GetWholeServiceRequirementsAsync(Guid eventId, CancellationToken cancellationToken);
}