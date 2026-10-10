using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>The songs a rehearsal will practise, set by the Choir Director.</summary>
public interface IRehearsalSongService
{
    /// <summary>The programme of the rehearsal in display order; empty when none was set.</summary>
    Task<Result<List<RehearsalSongDto>>> GetAsync(Guid rehearsalId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the programme with the listed songs, in the order given. A missing song fails with
    /// SONG_NOT_FOUND. An inactive song fails with SONG_INACTIVE unless it was already on the programme.
    /// </summary>
    Task<Result<List<RehearsalSongDto>>> UpdateAsync(
        Guid rehearsalId, UpdateRehearsalSongsRequest request, CancellationToken cancellationToken);
}
