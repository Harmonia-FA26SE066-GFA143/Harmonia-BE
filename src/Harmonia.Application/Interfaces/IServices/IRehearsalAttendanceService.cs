using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Attendance at rehearsals and preparation sessions, taken by the Choir Director (UC-30 / FE-45).</summary>
public interface IRehearsalAttendanceService
{
    /// <summary>
    /// The attendance sheet of a rehearsal, ordered by name: every active member, plus anyone already
    /// recorded who is no longer active. A member not yet recorded has a null status.
    /// </summary>
    Task<Result<List<RehearsalAttendanceDto>>> GetAttendancesAsync(Guid rehearsalId, CancellationToken cancellationToken);

    /// <summary>
    /// Records or corrects the listed members' attendance, stamped with the calling director.
    /// Allowed from the rehearsal's start time onward, including after it ends; before that fails with
    /// REHEARSAL_NOT_STARTED. A missing member fails with MEMBER_NOT_FOUND, an inactive one with MEMBER_NOT_ACTIVE.
    /// </summary>
    Task<Result> RecordAsync(
        Guid directorUserId, Guid rehearsalId, RecordRehearsalAttendancesRequest request, CancellationToken cancellationToken);
}
