using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Fixed weekly Mass times of the parish: the Admin keeps them, the priest generates events from them.</summary>
public interface IMassScheduleService
{
    Task<Result<MassScheduleDto>> CreateAsync(CreateMassScheduleRequest request, CancellationToken cancellationToken);

    /// <summary>Every schedule, ordered by day (Sunday first) then time.</summary>
    Task<Result<List<MassScheduleDto>>> GetAllAsync(CancellationToken cancellationToken);

    Task<Result<MassScheduleDto>> UpdateAsync(
        Guid id, UpdateMassScheduleRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a Draft event for every active schedule on each matching day of the range. A schedule whose Mass type
    /// or location is switched off is skipped, and so is a day + time + location already taken by a non-cancelled
    /// event. The season is the active one covering the date, or none. Returns the created events, soonest first.
    /// </summary>
    Task<Result<List<LiturgicalEventDto>>> GenerateEventsAsync(
        GenerateLiturgicalEventsRequest request, CancellationToken cancellationToken);
}
