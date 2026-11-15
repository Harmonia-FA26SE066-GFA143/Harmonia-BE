using System.Globalization;
using System.Net;
using System.Text;
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
    IEmailSender emailSender,
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
        registration.SaintName = TrimToNull(request.SaintName);
        registration.Gender = request.Gender;
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
        var dtos = mapper.Map<List<MemberRegistrationDto>>(page.Items);
        if (dtos.Count > 0)
        {
            await FlagDuplicatesAsync(dtos, cancellationToken);
        }

        return Result<PagedList<MemberRegistrationDto>>.Success(new PagedList<MemberRegistrationDto>(
            dtos, page.PageNumber, page.PageSize, page.TotalCount));
    }

    /// <summary>
    /// Marks rows that may be the same person under another email: the same phone number, or the same full name and
    /// saint name (a full name alone repeats too often in a parish). Compared against every waiting row and account.
    /// </summary>
    private async Task FlagDuplicatesAsync(List<MemberRegistrationDto> dtos, CancellationToken cancellationToken)
    {
        // shortcut: loads every waiting row and account into memory to compare without accents; move to a normalized,
        // indexed column if the parish grows past a few thousand accounts.
        var registrations = await registrationRepository.ListAsync(_ => true, cancellationToken);
        var users = await userRepository.ListAsync(_ => true, cancellationToken);
        var candidates = registrations
            .Select(x => (Kind: "Registration", x.Id, x.FullName, x.Email, x.Phone, x.SaintName))
            .Concat(users.Select(x => (Kind: "User", x.Id, x.FullName, x.Email, x.Phone, x.SaintName)))
            .Select(x => (Row: x, Phone: NormalizePhone(x.Phone), Name: NameKey(x.FullName, x.SaintName)))
            .ToList();

        foreach (var dto in dtos)
        {
            var phone = NormalizePhone(dto.Phone);
            var name = NameKey(dto.FullName, dto.SaintName);
            dto.PossibleDuplicates = candidates
                .Where(x => !(x.Row.Kind == "Registration" && x.Row.Id == dto.Id))
                .Select(x => (x.Row, MatchedOn: phone is not null && x.Phone == phone ? "Phone"
                    : name is not null && x.Name == name ? "Name"
                    : null))
                .Where(x => x.MatchedOn is not null)
                .Select(x => new RegistrationDuplicateDto
                {
                    Kind = x.Row.Kind, Id = x.Row.Id, FullName = x.Row.FullName, Email = x.Row.Email,
                    MatchedOn = x.MatchedOn!,
                })
                .ToList();
        }
    }

    /// <summary>Digits only, with the +84 country code read as the leading 0; null when too short to compare.</summary>
    private static string? NormalizePhone(string? phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length == 11)
        {
            digits = "0" + digits[2..];
        }

        return digits.Length < 9 ? null : digits;
    }

    /// <summary>Full name and saint name without case, accents or extra spaces; null without a saint name.</summary>
    private static string? NameKey(string fullName, string? saintName) =>
        string.IsNullOrWhiteSpace(saintName) || string.IsNullOrWhiteSpace(fullName)
            ? null
            : $"{Fold(saintName)}|{Fold(fullName)}";

    private static string Fold(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var letters = decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        return string.Join(' ', new string(letters.ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
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
                SaintName = registration.SaintName,
                Gender = registration.Gender,
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

    public async Task<Result> DeleteAsync(
        Guid id, DeleteMemberRegistrationRequest request, CancellationToken cancellationToken)
    {
        var registration = await registrationRepository.GetByIdAsync(id, cancellationToken);
        if (registration is null)
        {
            return Result.Failure(ErrorCodes.MemberRegistrationNotFound);
        }

        registrationRepository.Remove(registration);
        await registrationRepository.SaveChangesAsync(cancellationToken);

        // The result is ignored on purpose: the request is already gone, and the sender logs the failure.
        await emailSender.SendAsync(
            registration.Email,
            "Harmonia - Kết quả đăng ký gia nhập ca đoàn",
            $"<p>Chào {WebUtility.HtmlEncode(registration.FullName)},</p>"
                + "<p>Rất tiếc, đơn đăng ký gia nhập ca đoàn của bạn chưa được chấp nhận.</p>"
                + $"<p>Lý do: {WebUtility.HtmlEncode(request.Reason.Trim())}</p>"
                + "<p>Nếu cần trao đổi thêm, vui lòng liên hệ giáo xứ.</p>",
            cancellationToken);

        return Result.Success();
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
