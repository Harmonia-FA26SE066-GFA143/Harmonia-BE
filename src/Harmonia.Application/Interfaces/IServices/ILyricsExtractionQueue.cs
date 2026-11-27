namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Wakes the background worker for a job already saved as Queued. The database stays the source of truth.</summary>
public interface ILyricsExtractionQueue
{
    void Enqueue(Guid jobId);
}
