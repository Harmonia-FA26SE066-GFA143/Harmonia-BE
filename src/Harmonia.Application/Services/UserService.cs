using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Services;

public class UserService(
	IUserRepository userRepository,
	IMemberProfileRepository memberProfileRepository,
	IPasswordHasherService passwordHasherService,
	IEmailSender emailSender,
	ICurrentUserService currentUserService,
	IMapper mapper) : IUserService
{
	public async Task<Result<PagedList<UserDto>>> SearchAsync(SearchUsersRequest request, CancellationToken ct)
	{
		var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();
		var page = await userRepository.SearchAsync(keyword, request, ct);

		return Result<PagedList<UserDto>>.Success(new PagedList<UserDto>(
			mapper.Map<List<UserDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
	}

	public async Task<Result<UserDto>> GetByIdAsync(Guid id, CancellationToken ct)
	{
		var user = await userRepository.GetByIdAsync(id, ct);

		return user is null
			? Result<UserDto>.Failure(ErrorCodes.UserNotFound)
			: Result<UserDto>.Success(mapper.Map<UserDto>(user));
	}

	public async Task<Result<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken ct)
	{
		if (await userRepository.ExistsByEmailAsync(request.Email, null, ct))
			return Result<UserDto>.Failure(ErrorCodes.UserEmailAlreadyExists);

		var role = await userRepository.GetRoleByNameAsync(request.RoleName, ct);
		if (role is null) return Result<UserDto>.Failure(ErrorCodes.RoleNotFound);

		var user = new User
		{
			Id = Guid.NewGuid(),
			Email = request.Email,
			FullName = request.FullName?.Trim() ?? string.Empty,
			Phone = NormalizePhone(request.Phone),
			RoleId = role.Id,
			IsActive = true,
			IsPasswordChangeRequired = true,
		};
		user.PasswordHash = passwordHasherService.HashPassword(user, request.Password);
		if (role.Name == RoleNames.ChoirMember)
			user.MemberProfile = NewMemberProfile(user.Id);

		await userRepository.AddAsync(user, ct);
		await userRepository.SaveChangesAsync(ct);

		// The result is ignored on purpose: the account exists and the Admin who typed the password can still
		// pass it on; the sender already logs the failure.
		await emailSender.SendAsync(
			user.Email,
			"Harmonia - Your account",
			"<p>An account has been created for you on Harmonia.</p>"
				+ $"<p>Email: <b>{WebUtility.HtmlEncode(user.Email)}</b><br/>Password: <b>{WebUtility.HtmlEncode(request.Password)}</b></p>"
				+ "<p>You will be asked to choose a new password the first time you sign in.</p>",
			ct);

		return Result<UserDto>.Success(mapper.Map<UserDto>(user));
	}

	public async Task<Result<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct)
	{
		var user = await userRepository.GetByIdAsync(id, ct);
		if (user is null) return Result<UserDto>.Failure(ErrorCodes.UserNotFound);

		if (await userRepository.ExistsByEmailAsync(request.Email, id, ct))
			return Result<UserDto>.Failure(ErrorCodes.UserEmailAlreadyExists);

		user.Email = request.Email;
		user.FullName = request.FullName?.Trim() ?? string.Empty;
		user.Phone = NormalizePhone(request.Phone);
		await userRepository.SaveChangesAsync(ct);
		return Result<UserDto>.Success(mapper.Map<UserDto>(user));
	}

	public Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken ct) => GetByIdAsync(userId, ct);

	public async Task<Result<UserDto>> UpdateMeAsync(Guid userId, UpdateMyUserRequest request, CancellationToken ct)
	{
		var user = await userRepository.GetByIdAsync(userId, ct);
		if (user is null) return Result<UserDto>.Failure(ErrorCodes.UserNotFound);

		user.FullName = request.FullName?.Trim() ?? string.Empty;
		user.Phone = NormalizePhone(request.Phone);
		await userRepository.SaveChangesAsync(ct);
		return Result<UserDto>.Success(mapper.Map<UserDto>(user));
	}

	public async Task<Result> DeactivateAsync(Guid id, CancellationToken ct)
	{
		var user = await userRepository.GetByIdAsync(id, ct);
		if (user is null) return Result.Failure(ErrorCodes.UserNotFound);

		if (id == currentUserService.UserId)
			return Result.Failure(ErrorCodes.UserCannotModifySelf);

		if (!user.IsActive)
			return Result.Failure(ErrorCodes.UserAlreadyInactive);

		if (user.Role.Name == RoleNames.Admin
			&& await userRepository.CountActiveAdminsAsync(ct) <= 1)
			return Result.Failure(ErrorCodes.UserLastAdmin);

		user.IsActive = false;
		await userRepository.RevokeAllRefreshTokensAsync(id, ct);
		await userRepository.SaveChangesAsync(ct);
		return Result.Success();
	}

	public async Task<Result> ActivateAsync(Guid id, CancellationToken ct)
	{
		var user = await userRepository.GetByIdAsync(id, ct);
		if (user is null) return Result.Failure(ErrorCodes.UserNotFound);

		if (user.IsActive) return Result.Failure(ErrorCodes.UserAlreadyActive);

		user.IsActive = true;
		await userRepository.SaveChangesAsync(ct);
		return Result.Success();
	}

	public async Task<Result<UserDto>> AssignRoleAsync(Guid id, AssignRoleRequest request, CancellationToken ct)
	{
		var user = await userRepository.GetByIdAsync(id, ct);
		if (user is null) return Result<UserDto>.Failure(ErrorCodes.UserNotFound);

		if (id == currentUserService.UserId)
			return Result<UserDto>.Failure(ErrorCodes.UserCannotModifySelf);

		var newRole = await userRepository.GetRoleByNameAsync(request.RoleName, ct);
		if (newRole is null) return Result<UserDto>.Failure(ErrorCodes.RoleNotFound);

		if (user.Role.Name == RoleNames.Admin && newRole.Name != RoleNames.Admin
			&& await userRepository.CountActiveAdminsAsync(ct) <= 1)
			return Result<UserDto>.Failure(ErrorCodes.UserLastAdmin);

		user.RoleId = newRole.Id;
		// A profile is kept when the member later changes role, so their history stays attached.
		if (newRole.Name == RoleNames.ChoirMember
			&& await memberProfileRepository.GetByUserIdAsync(id, ct) is null)
			await memberProfileRepository.AddAsync(NewMemberProfile(id), ct);
		// The old role lives on in every issued token; force a fresh sign-in.
		await userRepository.RevokeAllRefreshTokensAsync(id, ct);
		await userRepository.SaveChangesAsync(ct);
		return Result<UserDto>.Success(mapper.Map<UserDto>(user));
	}

	private static string? NormalizePhone(string? phone) => string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

	private static MemberProfile NewMemberProfile(Guid userId) => new()
	{
		Id = Guid.NewGuid(),
		UserId = userId,
		JoinedDate = VietnamTime.Today,
		Status = MemberStatus.Active,
	};
}
