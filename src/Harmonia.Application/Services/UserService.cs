using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Services;

public class UserService(
	IUserRepository userRepository,
	IMemberProfileRepository memberProfileRepository,
	IPasswordHasherService passwordHasherService,
	IEmailSender emailSender,
	ICurrentUserService currentUserService,
	IFileStorageService fileStorageService,
	IGenericRepository<Choir> choirRepository,
	IGenericRepository<LiturgicalEventChoir> eventChoirRepository,
	INotificationService notificationService,
	IMapper mapper) : IUserService
{
	private const string AvatarFolder = "avatars";
	private const long MaxAvatarSizeBytes = 2 * 1024 * 1024;
	private static readonly HashSet<string> AvatarExtensions = [".jpg", ".jpeg", ".png", ".webp"];

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
			SaintName = TrimToNull(request.SaintName),
			Gender = request.Gender,
			RoleId = role.Id,
			IsActive = true,
			IsPasswordChangeRequired = true,
		};
		var initialPassword = GenerateInitialPassword();
		user.PasswordHash = passwordHasherService.HashPassword(user, initialPassword);
		if (role.Name == RoleNames.ChoirMember)
			user.MemberProfile = NewMemberProfile(user.Id);

		await userRepository.AddAsync(user, ct);
		await userRepository.SaveChangesAsync(ct);

		// The result is ignored on purpose: the account exists, and if the email is lost the user can still get in
		// through forgot-password; the sender already logs the failure. The password is never logged or returned.
		await emailSender.SendAsync(
			user.Email,
			"Harmonia - Your account",
			"<p>An account has been created for you on Harmonia.</p>"
				+ $"<p>Email: <b>{WebUtility.HtmlEncode(user.Email)}</b><br/>Password: <b>{WebUtility.HtmlEncode(initialPassword)}</b></p>"
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
		user.SaintName = TrimToNull(request.SaintName);
		user.Gender = request.Gender ?? user.Gender;
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
		user.SaintName = TrimToNull(request.SaintName);
		user.Gender = request.Gender ?? user.Gender;
		await userRepository.SaveChangesAsync(ct);
		return Result<UserDto>.Success(mapper.Map<UserDto>(user));
	}

	public async Task<Result<UserDto>> UpdateMyAvatarAsync(Guid userId, FileContent? file, CancellationToken ct)
	{
		if (CheckAvatar(file) is { } fileError) return Result<UserDto>.Failure(fileError);

		var user = await userRepository.GetByIdAsync(userId, ct);
		if (user is null) return Result<UserDto>.Failure(ErrorCodes.UserNotFound);

		var upload = await fileStorageService.UploadAsync(file!.Content, file.FileName, AvatarFolder, isPrivate: false, ct);
		if (!upload.IsSuccess) return Result<UserDto>.Failure(upload.Code!);

		var oldPublicId = user.AvatarPublicId;
		user.AvatarUrl = upload.Value!.Url;
		user.AvatarPublicId = upload.Value.PublicId;
		try
		{
			await userRepository.SaveChangesAsync(ct);
		}
		catch
		{
			// The row still points at the old image, so the new one is the orphan.
			await fileStorageService.DeleteAsync(upload.Value.PublicId, isPrivate: false, CancellationToken.None);
			throw;
		}

		// Best effort: a failure only leaves an unused image behind.
		if (oldPublicId is not null)
			await fileStorageService.DeleteAsync(oldPublicId, isPrivate: false, CancellationToken.None);

		return Result<UserDto>.Success(mapper.Map<UserDto>(user));
	}

	public async Task<Result> DeleteMyAvatarAsync(Guid userId, CancellationToken ct)
	{
		var user = await userRepository.GetByIdAsync(userId, ct);
		if (user is null) return Result.Failure(ErrorCodes.UserNotFound);
		if (user.AvatarPublicId is null && user.AvatarUrl is null) return Result.Success();

		var oldPublicId = user.AvatarPublicId;
		user.AvatarUrl = null;
		user.AvatarPublicId = null;
		await userRepository.SaveChangesAsync(ct);

		if (oldPublicId is not null)
			await fileStorageService.DeleteAsync(oldPublicId, isPrivate: false, CancellationToken.None);
		return Result.Success();
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

		if (user.Role.Name == RoleNames.ChoirDirector && await IsStillDirectingAsync(id, ct))
			return Result.Failure(ErrorCodes.UserStillDirecting);

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

		// A director leaving the role would leave their choirs, or an upcoming event they stand in for, with nobody.
		if (user.Role.Name == RoleNames.ChoirDirector && newRole.Name != RoleNames.ChoirDirector
			&& await IsStillDirectingAsync(id, ct))
			return Result<UserDto>.Failure(ErrorCodes.UserStillDirecting);

		var wasChoirMember = user.Role.Name == RoleNames.ChoirMember;
		user.RoleId = newRole.Id;
		var profile = await memberProfileRepository.GetForRoleChangeAsync(id, ct);
		List<Guid> leftChoirIds = [];
		if (newRole.Name == RoleNames.ChoirMember)
		{
			if (profile is null)
				await memberProfileRepository.AddAsync(NewMemberProfile(id), ct);
			else if (!wasChoirMember && profile.Status == MemberStatus.Inactive)
				profile.Status = MemberStatus.Active;
		}
		else if (profile is not null)
		{
			// Only Choir Members sing: the profile is kept for its history but leaves every choir and member flow.
			if (profile.Status == MemberStatus.Active) profile.Status = MemberStatus.Inactive;
			leftChoirIds = profile.ChoirMemberships.Where(m => m.IsActive).Select(m => m.ChoirId).ToList();
			foreach (var membership in profile.ChoirMemberships) membership.IsActive = false;
		}
		// The old role lives on in every issued token; force a fresh sign-in.
		await userRepository.RevokeAllRefreshTokensAsync(id, ct);
		await userRepository.SaveChangesAsync(ct);

		await NotifyDirectorsOfLeaveAsync(user, leftChoirIds, ct);
		return Result<UserDto>.Success(mapper.Map<UserDto>(user));
	}

	/// <summary>True while the user leads a choir or stands in as director at an event that has not happened yet.</summary>
	private async Task<bool> IsStillDirectingAsync(Guid userId, CancellationToken ct)
	{
		if ((await choirRepository.ListAsync(x => x.DirectorId == userId, ct)).Count > 0) return true;

		var today = VietnamTime.Today;
		return (await eventChoirRepository.ListAsync(
				x => x.SubstituteDirectorId == userId
					&& x.LiturgicalEvent.Status != EventStatus.Cancelled
					&& x.LiturgicalEvent.EventDate >= today,
				ct))
			.Count > 0;
	}

	/// <summary>Tells each choir's director that a member left it because their role changed.</summary>
	private async Task NotifyDirectorsOfLeaveAsync(User user, List<Guid> choirIds, CancellationToken ct)
	{
		if (choirIds.Count == 0) return;

		var choirs = await choirRepository.ListAsync(x => choirIds.Contains(x.Id) && x.DirectorId != null, ct);
		foreach (var choir in choirs)
		{
			var name = string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName;
			await notificationService.SendAsync(
				new SendNotificationRequest(
					NotificationType.ChoirChanged, "Ca viên rời ca đoàn",
					$"{name} đã rời ca đoàn {choir.Name} vì được đổi sang vai trò khác.",
					[choir.DirectorId!.Value], nameof(Choir), choir.Id),
				ct);
		}
	}

	// No look-alike characters (0/O, 1/l/I), since people retype it from an email.
	private const string InitialPasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

	/// <summary>Random 12-character first password with upper case, lower case and a digit, so it passes StrongPassword.</summary>
	private static string GenerateInitialPassword()
	{
		string password;
		do password = RandomNumberGenerator.GetString(InitialPasswordAlphabet, 12);
		while (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit));
		return password;
	}

	private static string? CheckAvatar(FileContent? file)
	{
		if (file is null || file.Length == 0) return ErrorCodes.AvatarFileRequired;

		var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
		if (!AvatarExtensions.Contains(extension)) return ErrorCodes.AvatarFileTypeNotAllowed;

		return file.Length > MaxAvatarSizeBytes ? ErrorCodes.AvatarFileTooLarge : null;
	}

	private static string? NormalizePhone(string? phone) => string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

	private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

	private static MemberProfile NewMemberProfile(Guid userId) => new()
	{
		Id = Guid.NewGuid(),
		UserId = userId,
		JoinedDate = VietnamTime.Today,
		Status = MemberStatus.Active,
	};
}
