using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>People needed per song of an event, per vocal part / instrument / role (UC-24 / FE-35).</summary>
public interface ISongPersonnelRequirementService
{
    /// <summary>
    /// Requirements of one song list item, ordered by skill name. A ChoirMember only sees items of an
    /// Approved song list of a Published event; anything else is SONG_LIST_ITEM_NOT_FOUND.
    /// </summary>
    Task<Result<List<SongPersonnelRequirementDto>>> GetAsync(Guid songListItemId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the item's whole requirement set. Only the latest song list version is editable, and not
    /// once the event's roster is finalized. Only newly added skills must be active.
    /// </summary>
    Task<Result<List<SongPersonnelRequirementDto>>> UpdateAsync(
        Guid songListItemId, UpdateSongPersonnelRequirementsRequest request, CancellationToken cancellationToken);
}
