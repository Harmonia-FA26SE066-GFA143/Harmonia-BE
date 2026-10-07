using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

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

    /// <summary>Same scope and loading as <see cref="GetForMemberAsync"/>; null when the member does not receive it.</summary>
    Task<PracticeAssignment?> GetByIdForMemberAsync(Guid id, Guid memberId, CancellationToken cancellationToken);
}
