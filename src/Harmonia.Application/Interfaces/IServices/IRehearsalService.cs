using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IServices;

public interface IRehearsalService
{
    /// <summary>Rehearsals past, ongoing and upcoming for the Choir Director (B20), newest start first.</summary>
    Task<Result<PagedList<RehearsalDto>>> SearchAsync(SearchRehearsalsRequest request, CancellationToken cancellationToken);

    Task<Result<RehearsalDto>> CreateAsync(CreateRehearsalRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Edits time, place, event and note of a rehearsal that has not ended, then notifies every active
    /// member (S-05). Songs are edited separately.
    /// </summary>
    Task<Result<RehearsalDto>> UpdateAsync(Guid id, UpdateRehearsalRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a rehearsal with no attendance recorded; members are notified (S-05) unless it has already ended.
    /// </summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}