using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IServices;

public interface IUserService
{
    Task<Result<PagedList<UserDto>>> SearchAsync(SearchUsersRequest request, CancellationToken ct);
    Task<Result<UserDto>> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Result<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<Result<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct);
    Task<Result> ActivateAsync(Guid id, CancellationToken ct);
    Task<Result> DeactivateAsync(Guid id, CancellationToken ct);
    Task<Result<UserDto>> AssignRoleAsync(Guid id, AssignRoleRequest request, CancellationToken ct);

    /// <summary>The signed-in user's own account, for every role.</summary>
    Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken ct);

    /// <summary>Any signed-in user edits their own name and phone; empty name and phone are allowed.</summary>
    Task<Result<UserDto>> UpdateMeAsync(Guid userId, UpdateMyUserRequest request, CancellationToken ct);

    /// <summary>Replaces the caller's avatar with a public image; the previous image is removed from storage.</summary>
    Task<Result<UserDto>> UpdateMyAvatarAsync(Guid userId, FileContent? file, CancellationToken ct);

    /// <summary>Removes the caller's avatar; succeeds when there is none.</summary>
    Task<Result> DeleteMyAvatarAsync(Guid userId, CancellationToken ct);
}