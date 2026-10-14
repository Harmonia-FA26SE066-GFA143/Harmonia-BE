using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Reports for the Parish Priest and the Admin over past events (UC-16 / FE-22).</summary>
public interface IReportService
{
    /// <summary>Published events before today (Vietnam time), newest first.</summary>
    Task<Result<PagedList<ServiceHistoryDto>>> GetServiceHistoryAsync(
        SearchReportsRequest request, CancellationToken cancellationToken);

    /// <summary>Every song of the library with its use count over the same events, most used first.</summary>
    Task<Result<PagedList<SongUsageDto>>> GetSongUsageAsync(
        SearchReportsRequest request, CancellationToken cancellationToken);

    /// <summary>Admin only (UC-34 / FE-52). Every account, ordered by email; the season filter does not apply.</summary>
    Task<Result<PagedList<UserActivityDto>>> GetUserActivityAsync(
        SearchReportsRequest request, CancellationToken cancellationToken);

    /// <summary>Admin only (UC-34 / FE-52). Active members, ordered by name.</summary>
    Task<Result<PagedList<RehearsalAttendanceReportDto>>> GetRehearsalAttendanceAsync(
        SearchReportsRequest request, CancellationToken cancellationToken);

    /// <summary>Admin only (UC-34 / FE-52). Active members, ordered by name.</summary>
    Task<Result<PagedList<ParticipationReportDto>>> GetParticipationAsync(
        SearchReportsRequest request, CancellationToken cancellationToken);

    /// <summary>Admin only (UC-34 / FE-52). Active members, ordered by name; assignments are picked by due date.</summary>
    Task<Result<PagedList<PracticeAssignmentCompletionDto>>> GetAssignmentCompletionAsync(
        SearchReportsRequest request, CancellationToken cancellationToken);
}
