using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class MemberRegistrationService(
    IMemberRegistrationRepository registrationRepository,
    IUserRepository userRepository,
    IUserService userService,
    INotificationService notificationService,
    IMapper mapper) : IMemberRegistrationService
{
    public async Task<Result> SubmitAsync(CreateMemberRegistrationRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        // Same answer as a new registration, so the public form cannot be used to probe for accounts.
        if (await userRepository.ExistsByEmailAsync(email, null, cancellationToken))
        {
            return Result.Success();
        }

        var registration = await registrationRepository.GetByEmailAsync(email, cancellationToken);
        var isNew = registration is null;
        if (registration is null)
        {
            registration = new MemberRegistration { Id = Guid.NewGuid(), Email = email };
            await registrationRepository.AddAsync(registration, cancellationToken);
        }

        registration.FullName = request.FullName.Trim();
        registration.Phone = TrimToNull(request.Phone);
        registration.Note = TrimToNull(request.Note);
        await registrationRepository.SaveChangesAsync(cancellationToken);

        // Only a new row is news to the Admins; a resubmission just refreshes one they already saw.
        if (isNew)
        {
            var adminIds = await userRepository.GetActiveUserIdsByRolesAsync([RoleNames.Admin], cancellationToken);
            await notificationService.SendAsync(
                new SendNotificationRequest(
                    NotificationType.MemberRegistrationSubmitted,
                    "Đăng ký gia nhập mới",
                    $"{registration.FullName} vừa đăng ký gia nhập ca đoàn.",
                    adminIds, nameof(MemberRegistration), registration.Id),
                cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result<PagedList<MemberRegistrationDto>>> SearchAsync(
        SearchMemberRegistrationsRequest request, CancellationToken cancellationToken)
    {
        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();
        var page = await registrationRepository.SearchAsync(keyword, request, cancellationToken);

        return Result<PagedList<MemberRegistrationDto>>.Success(new PagedList<MemberRegistrationDto>(
            mapper.Map<List<MemberRegistrationDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<UserDto>> AcceptAsync(Guid id, CancellationToken cancellationToken)
    {
        var registration = await registrationRepository.GetByIdAsync(id, cancellationToken);
        if (registration is null)
        {
            return Result<UserDto>.Failure(ErrorCodes.MemberRegistrationNotFound);
        }

        // Account creation, the first password and its email stay in one place: UserService.
        var created = await userService.CreateAsync(
            new CreateUserRequest
            {
                Email = registration.Email,
                FullName = registration.FullName,
                Phone = registration.Phone,
                RoleName = RoleNames.ChoirMember,
            },
            cancellationToken);
        if (!created.IsSuccess)
        {
            return created;
        }

        registrationRepository.Remove(registration);
        await registrationRepository.SaveChangesAsync(cancellationToken);

        return created;
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var registration = await registrationRepository.GetByIdAsync(id, cancellationToken);
        if (registration is null)
        {
            return Result.Failure(ErrorCodes.MemberRegistrationNotFound);
        }

        registrationRepository.Remove(registration);
        await registrationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
