using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IRehearsalRecordingRepository : IGenericRepository<RehearsalRecording>
{
    /// <summary>Recordings of the rehearsal with their Song loaded, newest first.</summary>
    Task<List<RehearsalRecording>> GetByRehearsalAsync(Guid rehearsalId, CancellationToken cancellationToken);
}
