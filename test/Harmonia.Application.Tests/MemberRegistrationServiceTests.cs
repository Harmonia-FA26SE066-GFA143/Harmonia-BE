using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Application.Mappings;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class MemberRegistrationServiceTests
{
    private readonly IMemberRegistrationRepository _registrations = Substitute.For<IMemberRegistrationRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IGenericRepository<Choir> _choirs = Substitute.For<IGenericRepository<Choir>>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly MemberRegistrationService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly List<MemberRegistration> _added = [];

    public MemberRegistrationServiceTests()
    {
        var mapper = new MapperConfiguration(
                cfg => cfg.AddProfile<MemberRegistrationProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new MemberRegistrationService(_registrations, _choirs, _users, _userService, _notifications, _email, mapper);

        _users.GetActiveUserIdsByRolesAsync(Arg.Any<IReadOnlyCollection<string>>(), _ct).Returns([_adminId]);
        _registrations.AddAsync(Arg.Do<MemberRegistration>(_added.Add), _ct).Returns(Task.CompletedTask);
    }

    private static CreateMemberRegistrationRequest NewRequest() => new()
    {
        FullName = "  Nguyen Van A  ", Email = " a@example.com ", Phone = " ", Note = " Cecilia choir ",
    };

    [Fact]
    public async Task Submit_NewEmail_StoresTrimmedRow_AndNotifiesAdmins_Async()
    {
        var result = await _sut.SubmitAsync(NewRequest(), _ct);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(_added);
        Assert.Equal("Nguyen Van A", row.FullName);
        Assert.Equal("a@example.com", row.Email);
        Assert.Null(row.Phone);
        Assert.Equal("Cecilia choir", row.Note);
        await _registrations.Received(1).SaveChangesAsync(_ct);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(r => r.Type == NotificationType.MemberRegistrationSubmitted
                && r.RecipientUserIds.SequenceEqual(new[] { _adminId })
                && r.ReferenceType == nameof(MemberRegistration) && r.ReferenceId == row.Id),
            _ct);
    }

    [Fact]
    public async Task Submit_EmailWithAccount_SucceedsWithoutStoringOrNotifying_Async()
    {
        _users.ExistsByEmailAsync("a@example.com", null, _ct).Returns(true);

        var result = await _sut.SubmitAsync(NewRequest(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(_added);
        await _registrations.DidNotReceive().SaveChangesAsync(_ct);
        await _notifications.DidNotReceive().SendAsync(Arg.Any<SendNotificationRequest>(), _ct);
    }

    [Fact]
    public async Task Submit_WaitingEmail_OverwritesRow_WithoutNotifying_Async()
    {
        var waiting = new MemberRegistration { Id = Guid.NewGuid(), Email = "a@example.com", FullName = "Old" };
        _registrations.GetByEmailAsync("a@example.com", _ct).Returns(waiting);

        var result = await _sut.SubmitAsync(NewRequest(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(_added);
        Assert.Equal("Nguyen Van A", waiting.FullName);
        await _registrations.Received(1).SaveChangesAsync(_ct);
        await _notifications.DidNotReceive().SendAsync(Arg.Any<SendNotificationRequest>(), _ct);
    }

    [Fact]
    public async Task Accept_CreatesChoirMemberAccount_ThenDeletesRegistration_Async()
    {
        var registration = new MemberRegistration
        {
            Id = Guid.NewGuid(), Email = "a@example.com", FullName = "Nguyen Van A", Phone = "0900000000", Gender = Gender.Male,
        };
        _registrations.GetByIdAsync(registration.Id, _ct).Returns(registration);
        var user = new UserDto { Email = registration.Email };
        _userService.CreateAsync(Arg.Any<CreateUserRequest>(), _ct).Returns(Result<UserDto>.Success(user));

        var result = await _sut.AcceptAsync(registration.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Same(user, result.Value);
        await _userService.Received(1).CreateAsync(
            Arg.Is<CreateUserRequest>(r => r.Email == "a@example.com" && r.FullName == "Nguyen Van A"
                && r.Phone == "0900000000" && r.Gender == Gender.Male && r.RoleName == RoleNames.ChoirMember),
            _ct);
        _registrations.Received(1).Remove(registration);
        await _registrations.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Submit_WithActiveChoir_StoresIt_Async()
    {
        var choir = new Choir { Id = Guid.NewGuid(), Name = "Cecilia", IsActive = true };
        _choirs.GetByIdAsync(choir.Id, _ct).Returns(choir);
        var request = NewRequest();
        request.PreferredChoirId = choir.Id;

        var result = await _sut.SubmitAsync(request, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(choir.Id, Assert.Single(_added).PreferredChoirId);
    }

    [Fact]
    public async Task Submit_InactiveOrUnknownChoir_ReturnsNotFound_AndStoresNothing_Async()
    {
        var closed = new Choir { Id = Guid.NewGuid(), Name = "Closed", IsActive = false };
        _choirs.GetByIdAsync(closed.Id, _ct).Returns(closed);
        var request = NewRequest();

        foreach (var choirId in new[] { closed.Id, Guid.NewGuid() })
        {
            request.PreferredChoirId = choirId;
            var result = await _sut.SubmitAsync(request, _ct);
            Assert.Equal(ErrorCodes.ChoirNotFound, result.Code);
        }

        Assert.Empty(_added);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Accept_PassesThePreferredChoir_OnlyWhileItIsActive_Async(bool isActive)
    {
        var choir = new Choir { Id = Guid.NewGuid(), Name = "Cecilia", IsActive = isActive };
        _choirs.GetByIdAsync(choir.Id, _ct).Returns(choir);
        var registration = new MemberRegistration
        {
            Id = Guid.NewGuid(), Email = "a@example.com", FullName = "A", PreferredChoirId = choir.Id,
        };
        _registrations.GetByIdAsync(registration.Id, _ct).Returns(registration);
        _userService.CreateAsync(Arg.Any<CreateUserRequest>(), _ct).Returns(Result<UserDto>.Success(new UserDto()));

        await _sut.AcceptAsync(registration.Id, _ct);

        Guid? expected = isActive ? choir.Id : null;
        await _userService.Received(1).CreateAsync(Arg.Is<CreateUserRequest>(r => r.PreferredChoirId == expected), _ct);
    }

    [Fact]
    public async Task Accept_AccountCreationFails_KeepsRegistration_Async()
    {
        var registration = new MemberRegistration { Id = Guid.NewGuid(), Email = "a@example.com", FullName = "A" };
        _registrations.GetByIdAsync(registration.Id, _ct).Returns(registration);
        _userService.CreateAsync(Arg.Any<CreateUserRequest>(), _ct)
            .Returns(Result<UserDto>.Failure(ErrorCodes.UserEmailAlreadyExists));

        var result = await _sut.AcceptAsync(registration.Id, _ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.UserEmailAlreadyExists, result.Code);
        _registrations.DidNotReceive().Remove(Arg.Any<MemberRegistration>());
    }

    [Fact]
    public async Task Accept_UnknownId_ReturnsNotFound_Async()
    {
        var result = await _sut.AcceptAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.MemberRegistrationNotFound, result.Code);
        await _userService.DidNotReceive().CreateAsync(Arg.Any<CreateUserRequest>(), _ct);
    }

    [Fact]
    public async Task Delete_RemovesRegistration_AndEmailsReasonToApplicant_Async()
    {
        var registration = new MemberRegistration { Id = Guid.NewGuid(), Email = "a@example.com", FullName = "A" };
        _registrations.GetByIdAsync(registration.Id, _ct).Returns(registration);

        var result = await _sut.DeleteAsync(
            registration.Id, new DeleteMemberRegistrationRequest { Reason = " <Đủ người> " }, _ct);

        Assert.True(result.IsSuccess);
        _registrations.Received(1).Remove(registration);
        await _registrations.Received(1).SaveChangesAsync(_ct);
        await _email.Received(1).SendAsync(
            "a@example.com", Arg.Any<string>(), Arg.Is<string>(body => body.Contains("&lt;Đủ người&gt;")), _ct);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound_AndSendsNoEmail_Async()
    {
        var result = await _sut.DeleteAsync(
            Guid.NewGuid(), new DeleteMemberRegistrationRequest { Reason = "x" }, _ct);

        Assert.Equal(ErrorCodes.MemberRegistrationNotFound, result.Code);
        await _email.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, _ct);
    }

    [Fact]
    public async Task Search_FlagsSamePhoneOrSameNameAndSaintName_AgainstRowsAndAccounts_Async()
    {
        var row = new MemberRegistration
        {
            Id = Guid.NewGuid(), FullName = "Nguyễn Thị Lan", SaintName = "Maria", Email = "lan2@example.com",
            Phone = "+84 912.345.678",
        };
        var samePhone = new MemberRegistration { Id = Guid.NewGuid(), FullName = "Lan N", Email = "x@example.com", Phone = "0912345678" };
        var nameOnly = new MemberRegistration { Id = Guid.NewGuid(), FullName = "Nguyen Thi Lan", Email = "y@example.com" };
        var account = new User { Id = Guid.NewGuid(), FullName = "nguyễn  thị LAN", SaintName = "maria", Email = "lan@example.com" };
        _registrations.SearchAsync(null, Arg.Any<SearchMemberRegistrationsRequest>(), _ct)
            .Returns(new PagedList<MemberRegistration>([row], 1, 20, 1));
        _registrations.ListAsync(default!, default).ReturnsForAnyArgs([row, samePhone, nameOnly]);
        _users.ListAsync(default!, default).ReturnsForAnyArgs([account]);

        var result = await _sut.SearchAsync(new SearchMemberRegistrationsRequest(), _ct);

        // The name-only row has no saint name, and a full name alone repeats too often to flag.
        var flags = Assert.Single(result.Value!.Items).PossibleDuplicates;
        Assert.Equal(
            [("Registration", samePhone.Id, "Phone"), ("User", account.Id, "Name")],
            flags.Select(x => (x.Kind, x.Id, x.MatchedOn)));
    }
}
