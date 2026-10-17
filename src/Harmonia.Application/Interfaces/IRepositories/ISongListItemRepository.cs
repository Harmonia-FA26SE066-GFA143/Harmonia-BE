using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface ISongListItemRepository : IGenericRepository<SongListItem>
{
    /// <summary>
    /// Tracked, with <see cref="SongListItem.PersonnelRequirements"/> (each with its Skill),
    /// <see cref="SongListItem.SongList"/>, its LiturgicalEvent and that event's ServiceRoster loaded.
    /// </summary>
    Task<SongListItem?> GetWithPersonnelRequirementsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>True when no song list of the event has a higher version than <paramref name="version"/>.</summary>
    Task<bool> IsLatestVersionAsync(Guid eventId, int version, CancellationToken cancellationToken);

    /// <summary>Tracked skills keyed by id. Missing ids are absent from the result.</summary>
    Task<Dictionary<Guid, Skill>> GetSkillsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}
