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

public class DirectorNominationServiceTests
{
    private readonly IDirectorNominationRepository _nominations = Substitute.For<IDirectorNominationRepository>();
    private readonly IMemberProfileRepository _members = Substitute.For<IMemberProfileRepository>();
    private readonly IGenericRepository<Choir> _choirs = Substitute.For<IGenericRepository<Choir>>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IChoirScopeService _scope = Substitute.For<IChoirScopeService>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IChoirService _choirService = Substitute.For<IChoirService>();
    private readonly ISystemSettingService _settings = Substitute.For<ISystemSettingService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly DirectorNominationService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly Guid _directorId = Guid.NewGuid();
    private readonly Guid _priestId = Guid.NewGuid();
    private readonly Guid _choirId = Guid.NewGuid();
    private readonly MemberProfile _member;
    private readonly Dictionary<Guid, DirectorNomination> _stored = [];

    public DirectorNominationServiceTests()
    {
        var mapper = new MapperConfiguration(
                cfg => cfg.AddProfile<DirectorNominationProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new DirectorNominationService(_nominations, _members, _choirs, _users, _scope, _userService,
            _choirService, _settings, _notifications, _currentUser, mapper);

        var memberUser = new User { Id = Guid.NewGuid(), FullName = "Member A", IsActive = true };
        _member = new MemberProfile { Id = Guid.NewGuid(), UserId = memberUser.Id, User = memberUser, Status = MemberStatus.Active };
        _members.GetWithUserAsync(_member.Id, _ct).Returns(_member);
        _members.GetByIdAsync(_member.Id, _ct).Returns(_member);

        _settings.GetIntAsync(SystemSettingKeys.NominationMinTenureMonths, _ct).Returns(24);
        _settings.GetIntAsync(SystemSettingKeys.NominationMinAttendancePercent, _ct).Returns(80);
        _settings.GetIntAsync(SystemSettingKeys.NominationMinServedEvents, _ct).Returns(20);
        _settings.GetBoolAsync(SystemSettingKeys.NominationRequireConductingSkill, _ct).Returns(true);
        _nominations.GetMemberStatsAsync(_member.Id, Arg.Any<DateTime>(), Arg.Any<DateOnly>(), _ct)
            .Returns(new MemberServiceStats(VietnamTime.Today.AddYears(-3), 10, 9, 25, true));
        _users.GetActiveUserIdsByRolesAsync(Arg.Any<IReadOnlyCollection<string>>(), _ct).Returns([_priestId]);

        _nominations.AddAsync(Arg.Do<DirectorNomination>(n => _stored[n.Id] = n), _ct).Returns(Task.CompletedTask);
        _nominations.GetByIdAsync(Arg.Any<Guid>(), _ct)
            .Returns(call => _stored.GetValueOrDefault(call.Arg<Guid>()));
        _nominations.GetWithDetailsAsync(Arg.Any<Guid>(), null, null, _ct).Returns(call =>
        {
            var n = _stored.GetValueOrDefault(call.Arg<Guid>());
            if (n is null) return null;
            n.Member = _member;
            n.NominatedBy = new User { Id = n.NominatedById, FullName = "Nominator" };
            return n;
        });

        ActAs(RoleNames.ChoirDirector, _directorId);
        _scope.GetDirectedChoirIdsAsync(_ct).Returns([_choirId]);
        _scope.GetMemberChoirIdsAsync(_member.Id, _ct).Returns([_choirId]);
    }

    private void ActAs(string role, Guid userId)
    {
        _currentUser.RoleName.Returns(role);
        _currentUser.UserId.Returns(userId);
    }

    private DirectorNomination Store(DirectorNominationStatus status)
    {
        var nomination = new DirectorNomination
        {
            Id = Guid.NewGuid(), MemberId = _member.Id, NominatedById = _directorId, Reason = "Leads well", Status = status,
        };
        _stored[nomination.Id] = nomination;
        return nomination;
    }

    private CreateDirectorNominationRequest NewRequest() => new() { MemberId = _member.Id, Reason = "  Leads well  " };

    [Fact]
    public async Task Create_ByDirectorForOwnChoirMember_StoresPending_AndNotifiesPriests_Async()
    {
        var result = await _sut.CreateAsync(NewRequest(), _ct);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(_stored.Values);
        Assert.Equal(DirectorNominationStatus.Pending, stored.Status);
        Assert.Equal(_directorId, stored.NominatedById);
        Assert.Equal("Leads well", stored.Reason);
        Assert.True(result.Value!.Stats.MeetsAll);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(r => r.Type == NotificationType.DirectorNomination
                && r.RecipientUserIds.SequenceEqual(new[] { _priestId }) && r.ReferenceId == stored.Id),
            _ct);
    }

    [Fact]
    public async Task Create_MemberOutsideDirectorsChoirs_ReturnsMemberNotFound_Async()
    {
        _scope.GetMemberChoirIdsAsync(_member.Id, _ct).Returns([Guid.NewGuid()]);

        var result = await _sut.CreateAsync(NewRequest(), _ct);

        Assert.Equal(ErrorCodes.MemberNotFound, result.Code);
        Assert.Empty(_stored);
    }

    [Fact]
    public async Task Create_ByPriest_SkipsChoirScope_AndNotifiesNobody_Async()
    {
        ActAs(RoleNames.ParishPriest, _priestId);
        _scope.GetDirectedChoirIdsAsync(_ct).Returns((IReadOnlyCollection<Guid>?)null);
        _scope.GetMemberChoirIdsAsync(_member.Id, _ct).Returns([]);

        var result = await _sut.CreateAsync(NewRequest(), _ct);

        Assert.True(result.IsSuccess);
        await _notifications.DidNotReceive().SendAsync(Arg.Any<SendNotificationRequest>(), _ct);
    }

    [Fact]
    public async Task Create_InactiveMember_ReturnsMemberNotActive_Async()
    {
        _member.Status = MemberStatus.Inactive;

        var result = await _sut.CreateAsync(NewRequest(), _ct);

        Assert.Equal(ErrorCodes.MemberNotActive, result.Code);
    }

    [Fact]
    public async Task Create_MemberWithOpenNomination_ReturnsAlreadyOpen_Async()
    {
        _nominations.HasOpenAsync(_member.Id, _ct).Returns(true);

        var result = await _sut.CreateAsync(NewRequest(), _ct);

        Assert.Equal(ErrorCodes.DirectorNominationAlreadyOpen, result.Code);
    }

    [Fact]
    public async Task Eligibility_BelowThresholds_FlagsEachOne_Async()
    {
        _nominations.GetMemberStatsAsync(_member.Id, Arg.Any<DateTime>(), Arg.Any<DateOnly>(), _ct)
            .Returns(new MemberServiceStats(VietnamTime.Today.AddMonths(-5), 4, 3, 2, false));

        var result = await _sut.GetEligibilityAsync(_member.Id, _ct);

        var stats = result.Value!;
        Assert.Equal(5, stats.TenureMonths);
        Assert.Equal(75, stats.AttendancePercent);
        Assert.False(stats.MeetsTenure);
        Assert.False(stats.MeetsAttendance);
        Assert.False(stats.MeetsServedEvents);
        Assert.False(stats.MeetsConductingSkill);
        Assert.False(stats.MeetsAll);
    }

    [Fact]
    public async Task Approve_Pending_SetsReviewer_AndNotifiesNominatorAndMember_Async()
    {
        ActAs(RoleNames.ParishPriest, _priestId);
        var nomination = Store(DirectorNominationStatus.Pending);

        var result = await _sut.ApproveAsync(nomination.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(DirectorNominationStatus.Approved, nomination.Status);
        Assert.Equal(_priestId, nomination.ReviewedById);
        Assert.NotNull(nomination.ReviewedAt);
        await _notifications.Received(2).SendAsync(Arg.Any<SendNotificationRequest>(), _ct);
    }

    [Fact]
    public async Task Approve_NotPending_ReturnsInvalidStatus_Async()
    {
        var nomination = Store(DirectorNominationStatus.Rejected);

        var result = await _sut.ApproveAsync(nomination.Id, _ct);

        Assert.Equal(ErrorCodes.DirectorNominationInvalidStatus, result.Code);
    }

    [Fact]
    public async Task Approve_UnknownId_ReturnsNotFound_Async()
    {
        var result = await _sut.ApproveAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.DirectorNominationNotFound, result.Code);
    }

    [Fact]
    public async Task Withdraw_AnotherDirectorsNomination_ReturnsNotFound_Async()
    {
        var nomination = Store(DirectorNominationStatus.Pending);
        ActAs(RoleNames.ChoirDirector, Guid.NewGuid());

        var result = await _sut.WithdrawAsync(nomination.Id, _ct);

        Assert.Equal(ErrorCodes.DirectorNominationNotFound, result.Code);
        Assert.Equal(DirectorNominationStatus.Pending, nomination.Status);
    }

    [Fact]
    public async Task Withdraw_OwnPending_Cancels_Async()
    {
        var nomination = Store(DirectorNominationStatus.Pending);

        var result = await _sut.WithdrawAsync(nomination.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(DirectorNominationStatus.Cancelled, nomination.Status);
    }

    [Fact]
    public async Task WaiveTraining_Approved_MovesToTrained_WithReason_Async()
    {
        ActAs(RoleNames.ParishPriest, _priestId);
        var nomination = Store(DirectorNominationStatus.Approved);

        var result = await _sut.WaiveTrainingAsync(
            nomination.Id, new ReviewDirectorNominationRequest { Note = " Diocesan certificate 2024 " }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(DirectorNominationStatus.Trained, nomination.Status);
        Assert.Equal("Diocesan certificate 2024", nomination.TrainingWaiverReason);
    }

    [Fact]
    public async Task Appoint_Trained_ChangesRole_ThenAssignsChoir_Async()
    {
        ActAs(RoleNames.Admin, Guid.NewGuid());
        var nomination = Store(DirectorNominationStatus.Trained);
        var choir = new Choir { Id = _choirId, Name = "Cecilia", Description = "Main", IsActive = true };
        _choirs.GetByIdAsync(_choirId, _ct).Returns(choir);
        _userService.AssignRoleAsync(_member.UserId, Arg.Any<AssignRoleRequest>(), _ct)
            .Returns(Result<UserDto>.Success(new UserDto()));
        _choirService.UpdateAsync(_choirId, Arg.Any<UpdateChoirRequest>(), _ct)
            .Returns(Result<ChoirDto>.Success(new ChoirDto()));

        var result = await _sut.AppointAsync(nomination.Id, new AppointDirectorNominationRequest { ChoirId = _choirId }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(DirectorNominationStatus.Appointed, nomination.Status);
        Assert.Equal(_choirId, nomination.AppointedChoirId);
        await _userService.Received(1).AssignRoleAsync(
            _member.UserId, Arg.Is<AssignRoleRequest>(r => r.RoleName == RoleNames.ChoirDirector), _ct);
        await _choirService.Received(1).UpdateAsync(
            _choirId,
            Arg.Is<UpdateChoirRequest>(r => r.DirectorId == _member.UserId && r.Name == "Cecilia"
                && r.Description == "Main" && r.IsActive),
            _ct);
    }

    [Fact]
    public async Task Appoint_RoleChangeFails_LeavesChoirAndNominationUntouched_Async()
    {
        ActAs(RoleNames.Admin, Guid.NewGuid());
        var nomination = Store(DirectorNominationStatus.Trained);
        _choirs.GetByIdAsync(_choirId, _ct).Returns(new Choir { Id = _choirId, Name = "Cecilia", IsActive = true });
        _userService.AssignRoleAsync(_member.UserId, Arg.Any<AssignRoleRequest>(), _ct)
            .Returns(Result<UserDto>.Failure(ErrorCodes.UserNotFound));

        var result = await _sut.AppointAsync(nomination.Id, new AppointDirectorNominationRequest { ChoirId = _choirId }, _ct);

        Assert.Equal(ErrorCodes.UserNotFound, result.Code);
        Assert.Equal(DirectorNominationStatus.Trained, nomination.Status);
        await _choirService.DidNotReceive().UpdateAsync(Arg.Any<Guid>(), Arg.Any<UpdateChoirRequest>(), _ct);
    }

    [Fact]
    public async Task Appoint_NotTrained_ReturnsInvalidStatus_Async()
    {
        ActAs(RoleNames.Admin, Guid.NewGuid());
        var nomination = Store(DirectorNominationStatus.Approved);

        var result = await _sut.AppointAsync(nomination.Id, new AppointDirectorNominationRequest { ChoirId = _choirId }, _ct);

        Assert.Equal(ErrorCodes.DirectorNominationInvalidStatus, result.Code);
        await _userService.DidNotReceive().AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<AssignRoleRequest>(), _ct);
    }

    [Fact]
    public async Task Search_AsMember_FiltersToOwnNominations_Async()
    {
        var memberUserId = Guid.NewGuid();
        ActAs(RoleNames.ChoirMember, memberUserId);
        _nominations.SearchAsync(Arg.Any<SearchDirectorNominationsRequest>(), null, memberUserId, _ct)
            .Returns(new PagedList<DirectorNomination>([], 1, 20, 0));

        var result = await _sut.SearchAsync(new SearchDirectorNominationsRequest(), _ct);

        Assert.True(result.IsSuccess);
        await _nominations.Received(1).SearchAsync(Arg.Any<SearchDirectorNominationsRequest>(), null, memberUserId, _ct);
    }
}
