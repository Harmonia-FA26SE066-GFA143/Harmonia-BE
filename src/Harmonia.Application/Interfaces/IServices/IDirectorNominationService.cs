using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Member → Choir Director nominations (UC-46). Who sees what: Admin and Parish Priest every nomination, a Choir
/// Director the ones they created, a Choir Member their own once the Parish Priest approved it. Anything else is 404.
/// </summary>
public interface IDirectorNominationService
{
    /// <summary>Track record of a member before nominating them; a Choir Director only for members of their choirs.</summary>
    Task<Result<DirectorNominationStatsDto>> GetEligibilityAsync(Guid memberId, CancellationToken cancellationToken);

    Task<Result<DirectorNominationDetailDto>> CreateAsync(
        CreateDirectorNominationRequest request, CancellationToken cancellationToken);

    Task<Result<PagedList<DirectorNominationDto>>> SearchAsync(
        SearchDirectorNominationsRequest request, CancellationToken cancellationToken);

    Task<Result<DirectorNominationDetailDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The nominator takes back a Pending nomination.</summary>
    Task<Result<DirectorNominationDetailDto>> WithdrawAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<DirectorNominationDetailDto>> ApproveAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<DirectorNominationDetailDto>> RejectAsync(
        Guid id, ReviewDirectorNominationRequest request, CancellationToken cancellationToken);

    /// <summary>The Parish Priest drops an Approved nomination.</summary>
    Task<Result<DirectorNominationDetailDto>> CancelAsync(
        Guid id, ReviewDirectorNominationRequest request, CancellationToken cancellationToken);

    /// <summary>Approved → Trained without training, with the reason recorded.</summary>
    Task<Result<DirectorNominationDetailDto>> WaiveTrainingAsync(
        Guid id, ReviewDirectorNominationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Trained → Appointed: the member's role becomes Choir Director, then they become the choir's director. Two saves:
    /// if the second fails the account is already a director without the choir, and the Admin assigns it on the choir.
    /// </summary>
    Task<Result<DirectorNominationDetailDto>> AppointAsync(
        Guid id, AppointDirectorNominationRequest request, CancellationToken cancellationToken);
}
