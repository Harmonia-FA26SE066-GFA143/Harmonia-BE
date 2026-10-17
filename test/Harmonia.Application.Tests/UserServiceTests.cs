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

public class UserServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IMemberProfileRepository _memberProfiles = Substitute.For<IMemberProfileRepository>();
    private readonly IPasswordHasherService _hasher = Substitute.For<IPasswordHasherService>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly UserService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly List<User> _added = [];
    private readonly Role _adminRole = new() { Id = Guid.NewGuid(), Name = RoleNames.Admin };
    private readonly Role _directorRole = new() { Id = Guid.NewGuid(), Name = RoleNames.ChoirDirector };
    private readonly Role _memberRole = new() { Id = Guid.NewGuid(), Name = RoleNames.ChoirMember };
    private readonly Guid _callerId = Guid.NewGuid();
    private string? _hashedPassword;

    public UserServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<UserProfile>(), NullLoggerFactory.Instance).CreateMapper();
        _sut = new UserService(_users, _memberProfiles, _hasher, _email, _currentUser, mapper);

        _currentUser.UserId.Returns(_callerId);
        _hasher.HashPassword(Arg.Any<User>(), Arg.Do<string>(p => _hashedPassword = p)).Returns("hashed");
        _email.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), _ct).Returns(Result.Success());
        _users.CountActiveAdminsAsync(_ct).Returns(2);
        foreach (var role in new[] { _adminRole, _directorRole, _memberRole })
        {
            _users.GetRoleByNameAsync(role.Name, _ct).Returns(role);
        }

        // EF fixes up the Role navigation from RoleId when the user is added; the substitute has to do it by hand.
        _users.AddAsync(
                Arg.Do<User>(user =>
                {
                    user.Role = new[] { _adminRole, _directorRole, _memberRole }.Single(r => r.Id == user.RoleId);
                    _added.Add(user);
                }),
                _ct)
            .Returns(Task.CompletedTask);
    }

    private User Existing(Role role, bool isActive = true)
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "user@example.com", FullName = "Old name", Phone = "0900000000",
            Role = role, RoleId = role.Id, IsActive = isActive,
        };
        _users.GetByIdAsync(user.Id, _ct).Returns(user);
        return user;
    }

    private static CreateUserRequest NewUser(string roleName) => new()
    {
        Email = "new@example.com", FullName = "  New user  ", Phone = " 0911111111 ", RoleName = roleName,
    };

    // ---- Search / get ----

    [Fact]
    public async Task Search_TrimsKeyword_AndKeepsPaging_Async()
    {
        var request = new SearchUsersRequest { Keyword = "  anna  " };
        var user = Existing(_directorRole);
        _users.SearchAsync("anna", request, _ct).Returns(new PagedList<User>([user], 2, 10, 11));

        var result = await _sut.SearchAsync(request, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal((2, 10, 11), (result.Value!.PageNumber, result.Value.PageSize, result.Value.TotalCount));
        var dto = Assert.Single(result.Value.Items);
        Assert.Equal((user.Id, "user@example.com", RoleNames.ChoirDirector), (dto.Id, dto.Email, dto.RoleName));
    }

    [Fact]
    public async Task Search_BlankKeyword_SearchesWithoutKeyword_Async()
    {
        var request = new SearchUsersRequest { Keyword = "   " };
        _users.SearchAsync(null, request, _ct).Returns(new PagedList<User>([], 1, 20, 0));

        var result = await _sut.SearchAsync(request, _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
    }

    [Fact]
    public async Task GetById_Unknown_ReturnsUserNotFound_Async()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.UserNotFound, result.Code);
    }

    [Fact]
    public async Task GetMe_ReturnsTheCallersAccount_Async()
    {
        var user = Existing(_memberRole);

        var result = await _sut.GetMeAsync(user.Id, _ct);

        Assert.Equal((user.Id, RoleNames.ChoirMember), (result.Value!.Id, result.Value.RoleName));
    }

    // ---- Create ----

    [Fact]
    public async Task Create_ChoirMember_StoresAccountWithProfile_AndEmailsTheGeneratedPassword_Async()
    {
        var result = await _sut.CreateAsync(NewUser(RoleNames.ChoirMember), _ct);

        Assert.True(result.IsSuccess);
        var user = Assert.Single(_added);
        Assert.Equal(("new@example.com", "New user", "0911111111", _memberRole.Id), (user.Email, user.FullName, user.Phone, user.RoleId));
        Assert.True(user.IsActive);
        Assert.True(user.IsPasswordChangeRequired);
        Assert.Equal("hashed", user.PasswordHash);
        Assert.Equal((user.Id, MemberStatus.Active), (user.MemberProfile!.UserId, user.MemberProfile.Status));
        await _users.Received(1).SaveChangesAsync(_ct);

        // The first password is strong, reaches the user by email only, and never comes back in the response.
        Assert.Equal(12, _hashedPassword!.Length);
        Assert.Contains(_hashedPassword, char.IsUpper);
        Assert.Contains(_hashedPassword, char.IsLower);
        Assert.Contains(_hashedPassword, char.IsDigit);
        await _email.Received(1).SendAsync(
            "new@example.com", Arg.Any<string>(), Arg.Is<string>(body => body.Contains(_hashedPassword)), _ct);
        Assert.Equal((user.Id, RoleNames.ChoirMember, true), (result.Value!.Id, result.Value.RoleName, result.Value.IsPasswordChangeRequired));
    }

    [Fact]
    public async Task Create_OtherRole_BlankNameAndPhone_HasNoMemberProfile_Async()
    {
        var request = new CreateUserRequest { Email = "priest@example.com", Phone = "  ", RoleName = RoleNames.ChoirDirector };

        var result = await _sut.CreateAsync(request, _ct);

        Assert.True(result.IsSuccess);
        var user = Assert.Single(_added);
        Assert.Null(user.MemberProfile);
        Assert.Equal(string.Empty, user.FullName);
        Assert.Null(user.Phone);
    }

    [Fact]
    public async Task Create_EmailFails_StillSucceeds_Async()
    {
        _email.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), _ct)
            .Returns(Result.Failure(ErrorCodes.ExternalEmailFailed));

        var result = await _sut.CreateAsync(NewUser(RoleNames.ChoirMember), _ct);

        Assert.True(result.IsSuccess);
        Assert.Single(_added);
    }

    [Fact]
    public async Task Create_EmailTaken_ReturnsEmailAlreadyExists_AndSavesNothing_Async()
    {
        _users.ExistsByEmailAsync("new@example.com", null, _ct).Returns(true);

        var result = await _sut.CreateAsync(NewUser(RoleNames.ChoirMember), _ct);

        Assert.Equal(ErrorCodes.UserEmailAlreadyExists, result.Code);
        Assert.Empty(_added);
        await _email.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, _ct);
    }

    [Fact]
    public async Task Create_UnknownRole_ReturnsRoleNotFound_Async()
    {
        var result = await _sut.CreateAsync(NewUser("Instrumentalist"), _ct);

        Assert.Equal(ErrorCodes.RoleNotFound, result.Code);
        Assert.Empty(_added);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_Valid_ChangesEmailNameAndPhone_Async()
    {
        var user = Existing(_directorRole);
        var request = new UpdateUserRequest { Email = "renamed@example.com", FullName = " Renamed ", Phone = " " };

        var result = await _sut.UpdateAsync(user.Id, request, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(("renamed@example.com", "Renamed", null), (user.Email, user.FullName, user.Phone));
        await _users.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Update_Unknown_ReturnsUserNotFound_Async()
    {
        var result = await _sut.UpdateAsync(Guid.NewGuid(), new UpdateUserRequest { Email = "a@example.com" }, _ct);

        Assert.Equal(ErrorCodes.UserNotFound, result.Code);
    }

    [Fact]
    public async Task Update_EmailOfAnotherUser_ReturnsEmailAlreadyExists_AndKeepsTheOldEmail_Async()
    {
        var user = Existing(_directorRole);
        _users.ExistsByEmailAsync("taken@example.com", user.Id, _ct).Returns(true);

        var result = await _sut.UpdateAsync(user.Id, new UpdateUserRequest { Email = "taken@example.com" }, _ct);

        Assert.Equal(ErrorCodes.UserEmailAlreadyExists, result.Code);
        Assert.Equal("user@example.com", user.Email);
        await _users.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task UpdateMe_ChangesNameAndPhoneOnly_Async()
    {
        var user = Existing(_memberRole);

        var result = await _sut.UpdateMeAsync(user.Id, new UpdateMyUserRequest { FullName = " Me ", Phone = " 0922222222 " }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(("user@example.com", "Me", "0922222222"), (user.Email, user.FullName, user.Phone));
        await _users.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task UpdateMe_Unknown_ReturnsUserNotFound_Async()
    {
        var result = await _sut.UpdateMeAsync(Guid.NewGuid(), new UpdateMyUserRequest(), _ct);

        Assert.Equal(ErrorCodes.UserNotFound, result.Code);
    }

    // ---- Deactivate / activate ----

    [Fact]
    public async Task Deactivate_Valid_TurnsOffAndRevokesEveryRefreshToken_Async()
    {
        var user = Existing(_adminRole);

        var result = await _sut.DeactivateAsync(user.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.False(user.IsActive);
        await _users.Received(1).RevokeAllRefreshTokensAsync(user.Id, _ct);
        await _users.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Deactivate_Unknown_ReturnsUserNotFound_Async()
    {
        var result = await _sut.DeactivateAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.UserNotFound, result.Code);
    }

    [Fact]
    public async Task Deactivate_Self_ReturnsCannotModifySelf_Async()
    {
        var user = Existing(_adminRole);
        _currentUser.UserId.Returns(user.Id);

        var result = await _sut.DeactivateAsync(user.Id, _ct);

        Assert.Equal(ErrorCodes.UserCannotModifySelf, result.Code);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task Deactivate_AlreadyInactive_ReturnsAlreadyInactive_Async()
    {
        var user = Existing(_directorRole, isActive: false);

        var result = await _sut.DeactivateAsync(user.Id, _ct);

        Assert.Equal(ErrorCodes.UserAlreadyInactive, result.Code);
        await _users.DidNotReceive().RevokeAllRefreshTokensAsync(user.Id, _ct);
    }

    [Fact]
    public async Task Deactivate_LastActiveAdmin_ReturnsLastAdmin_Async()
    {
        var user = Existing(_adminRole);
        _users.CountActiveAdminsAsync(_ct).Returns(1);

        var result = await _sut.DeactivateAsync(user.Id, _ct);

        Assert.Equal(ErrorCodes.UserLastAdmin, result.Code);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task Deactivate_NonAdminWhenOneAdminLeft_Succeeds_Async()
    {
        var user = Existing(_memberRole);
        _users.CountActiveAdminsAsync(_ct).Returns(1);

        var result = await _sut.DeactivateAsync(user.Id, _ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Activate_Inactive_TurnsOn_Async()
    {
        var user = Existing(_memberRole, isActive: false);

        var result = await _sut.ActivateAsync(user.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.True(user.IsActive);
        await _users.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Activate_AlreadyActive_ReturnsAlreadyActive_Async()
    {
        var user = Existing(_memberRole);

        var result = await _sut.ActivateAsync(user.Id, _ct);

        Assert.Equal(ErrorCodes.UserAlreadyActive, result.Code);
    }

    [Fact]
    public async Task Activate_Unknown_ReturnsUserNotFound_Async()
    {
        var result = await _sut.ActivateAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.UserNotFound, result.Code);
    }

    // ---- Assign role ----

    [Fact]
    public async Task AssignRole_ToChoirMemberWithoutProfile_AddsProfile_AndRevokesTokens_Async()
    {
        var user = Existing(_directorRole);

        var result = await _sut.AssignRoleAsync(user.Id, new AssignRoleRequest { RoleName = RoleNames.ChoirMember }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(_memberRole.Id, user.RoleId);
        await _memberProfiles.Received(1).AddAsync(
            Arg.Is<MemberProfile>(p => p.UserId == user.Id && p.Status == MemberStatus.Active), _ct);
        await _users.Received(1).RevokeAllRefreshTokensAsync(user.Id, _ct);
        await _users.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task AssignRole_ToChoirMemberWithProfile_KeepsTheExistingProfile_Async()
    {
        var user = Existing(_directorRole);
        _memberProfiles.GetByUserIdAsync(user.Id, _ct).Returns(new MemberProfile { Id = Guid.NewGuid(), UserId = user.Id });

        var result = await _sut.AssignRoleAsync(user.Id, new AssignRoleRequest { RoleName = RoleNames.ChoirMember }, _ct);

        Assert.True(result.IsSuccess);
        await _memberProfiles.DidNotReceiveWithAnyArgs().AddAsync(default!, _ct);
    }

    [Fact]
    public async Task AssignRole_ToOtherRole_AddsNoProfile_Async()
    {
        var user = Existing(_memberRole);

        var result = await _sut.AssignRoleAsync(user.Id, new AssignRoleRequest { RoleName = RoleNames.ChoirDirector }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(_directorRole.Id, user.RoleId);
        await _memberProfiles.DidNotReceiveWithAnyArgs().AddAsync(default!, _ct);
    }

    [Fact]
    public async Task AssignRole_Unknown_ReturnsUserNotFound_Async()
    {
        var result = await _sut.AssignRoleAsync(Guid.NewGuid(), new AssignRoleRequest { RoleName = RoleNames.Admin }, _ct);

        Assert.Equal(ErrorCodes.UserNotFound, result.Code);
    }

    [Fact]
    public async Task AssignRole_Self_ReturnsCannotModifySelf_Async()
    {
        var user = Existing(_adminRole);
        _currentUser.UserId.Returns(user.Id);

        var result = await _sut.AssignRoleAsync(user.Id, new AssignRoleRequest { RoleName = RoleNames.ChoirDirector }, _ct);

        Assert.Equal(ErrorCodes.UserCannotModifySelf, result.Code);
        Assert.Equal(_adminRole.Id, user.RoleId);
    }

    [Fact]
    public async Task AssignRole_UnknownRole_ReturnsRoleNotFound_Async()
    {
        var user = Existing(_directorRole);

        var result = await _sut.AssignRoleAsync(user.Id, new AssignRoleRequest { RoleName = "Instrumentalist" }, _ct);

        Assert.Equal(ErrorCodes.RoleNotFound, result.Code);
        await _users.DidNotReceive().RevokeAllRefreshTokensAsync(user.Id, _ct);
    }

    [Fact]
    public async Task AssignRole_DemotingLastActiveAdmin_ReturnsLastAdmin_Async()
    {
        var user = Existing(_adminRole);
        _users.CountActiveAdminsAsync(_ct).Returns(1);

        var result = await _sut.AssignRoleAsync(user.Id, new AssignRoleRequest { RoleName = RoleNames.ChoirDirector }, _ct);

        Assert.Equal(ErrorCodes.UserLastAdmin, result.Code);
        Assert.Equal(_adminRole.Id, user.RoleId);
    }

    [Fact]
    public async Task AssignRole_LastAdminKeepsAdmin_Succeeds_Async()
    {
        var user = Existing(_adminRole);
        _users.CountActiveAdminsAsync(_ct).Returns(1);

        var result = await _sut.AssignRoleAsync(user.Id, new AssignRoleRequest { RoleName = RoleNames.Admin }, _ct);

        Assert.True(result.IsSuccess);
    }
}
