using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Services;

public class UserService(
	IUserRepository userRepository,
	IPasswordHasherService passwordHasherService,
	ICurrentUserService currentUserService,
	IMapper mapper) : IUserService
{
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
			RoleId = role.Id,
			IsActive = true,
		};
		user.PasswordHash = passwordHasherService.HashPassword(user, request.Password);

		await userRepository.AddAsync(user, ct);
		await userRepository.SaveChangesAsync(ct);
		return Result<UserDto>.Success(mapper.Map<UserDto>(user));
	}

	public async Task<Result<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct)
	{
		var user = await userRepository.GetByIdAsync(id, ct);
		if (user is null) return Result<UserDto>.Failure(ErrorCodes.UserNotFound);

		if (await userRepository.ExistsByEmailAsync(request.Email, id, ct))
			return Result<UserDto>.Failure(ErrorCodes.UserEmailAlreadyExists);

		user.Email = request.Email;
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
		await userRepository.SaveChangesAsync(ct);
		return Result.Success();
	}

	public async Task<Result> ActivateAsync(Guid id, CancellationToken ct)
	{
		var user = await userRepository.GetByIdAsync(id, ct);
		if (user is null) return Result.Failure(ErrorCodes.UserNotFound);

		if (user.IsActive) return Result.Failure(ErrorCodes.UserAlreadyActive); // ho?c thêm ErrorCodes.UserAlreadyActive n?u team mu?n tách riêng

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
		await userRepository.SaveChangesAsync(ct);
		return Result<UserDto>.Success(mapper.Map<UserDto>(user));
	}
}