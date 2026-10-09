using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IPracticeAssignmentRepository : IGenericRepository<PracticeAssignment>
{
    /// <summary>
    /// Read-only page of the assignments the member currently receives: scope All, a target naming the member,
    /// or a target skill the member holds approved today. Ordered by due date, earliest first.
    /// <see cref="PracticeAssignment.Song"/>, <see cref="PracticeAssignment.Material"/> and
    /// <see cref="PracticeAssignment.LiturgicalEvent"/> are loaded; <see cref="PracticeAssignment.Submissions"/>
    /// holds only the member's newest submission, if any.
    /// </summary>
    Task<PagedList<PracticeAssignment>> GetForMemberAsync(
        Guid memberId, SearchMyPracticeAssignmentsRequest filter, CancellationToken cancellationToken);

    /// <summary>Due date and the member's newest attempt status of every assignment the member receives.</summary>
    Task<List<(DateTime DueDate, SubmissionStatus? LatestStatus)>> GetProgressForMemberAsync(
        Guid memberId, CancellationToken cancellationToken);

    /// <summary>Same scope and loading as <see cref="GetForMemberAsync"/>; null when the member does not receive it.</summary>
    Task<PracticeAssignment?> GetByIdForMemberAsync(Guid id, Guid memberId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only assignments of the event, with <see cref="PracticeAssignment.Targets"/> and every member's
    /// <see cref="PracticeAssignment.Submissions"/> loaded.
    /// </summary>
    Task<List<PracticeAssignment>> GetByEventWithSubmissionsAsync(Guid eventId, CancellationToken cancellationToken);
}
