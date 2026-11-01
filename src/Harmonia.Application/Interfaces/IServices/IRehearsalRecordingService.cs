using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Takes the Choir Director records in the rehearsal practice room (Web B43).</summary>
public interface IRehearsalRecordingService
{
    /// <summary>
    /// The director sees the recordings of rehearsals of their choirs; a member those of rehearsals they can see
    /// (their choirs, published event). Anything else reads as REHEARSAL_NOT_FOUND.
    /// </summary>
    Task<Result<List<RehearsalRecordingDto>>> GetAsync(
        Guid userId, bool isDirector, Guid rehearsalId, CancellationToken cancellationToken);

    Task<Result<RehearsalRecordingDto>> CreateAsync(
        Guid rehearsalId, CreateRehearsalRecordingRequest request, FileContent? file, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid rehearsalId, Guid recordingId, CancellationToken cancellationToken);
}
