using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

public interface IMemberRegistrationService
{
    /// <summary>
    /// Anonymous. Always succeeds so the response never reveals whether the email already has an account:
    /// an existing account is ignored and a waiting registration with the same email is overwritten.
    /// </summary>
    Task<Result> SubmitAsync(CreateMemberRegistrationRequest request, CancellationToken cancellationToken);

    Task<Result<PagedList<MemberRegistrationDto>>> SearchAsync(
        SearchMemberRegistrationsRequest request, CancellationToken cancellationToken);

    /// <summary>Creates a Choir Member account from the registration, then deletes the registration.</summary>
    Task<Result<UserDto>> AcceptAsync(Guid id, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
