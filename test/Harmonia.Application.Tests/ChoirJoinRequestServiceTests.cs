using System.Linq.Expressions;
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

public class ChoirJoinRequestServiceTests
{
    private readonly IChoirJoinRequestRepository _requests = Substitute.For<IChoirJoinRequestRepository>();
    private readonly IGenericRepository<Choir> _choirs = Substitute.For<IGenericRepository<Choir>>();
    private readonly IGenericRepository<ChoirMembership> _memberships = Substitute.For<IGenericRepository<ChoirMembership>>();
    private readonly IMemberProfileRepository _members = Substitute.For<IMemberProfileRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IChoirService _choirService = Substitute.For<IChoirService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ChoirJoinRequestService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly List<ChoirJoinRequest> _rows = [];
    private readonly List<ChoirMembership> _membershipRows = [];
    private readonly List<SendNotificationRequest> _sent = [];
    private readonly Guid _directorId = Guid.NewGuid();
    private readonly Choir _choir;
    private readonly MemberProfile _member;

    public ChoirJoinRequestServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<ChoirJoinRequestProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new ChoirJoinRequestService(
            _requests, _choirs, _memberships, _members, _users, _choirService, _notifications, _currentUser, mapper);

        _choir = new Choir { Id = Guid.NewGuid(), Name = "Cecilia", IsActive = true, DirectorId = _directorId };
        _member = NewMember("Anna");
        _choirs.GetByIdAsync(_choir.Id, _ct).Returns(_choir);

        // The fakes evaluate the service's predicate against in-memory rows, as the database would.
        _requests.ListAsync(Arg.Any<Expression<Func<ChoirJoinRequest, bool>>>(), _ct)
            .Returns(call => _rows.Where(call.Arg<Expression<Func<ChoirJoinRequest, bool>>>().Compile()).ToList());
        _requests.AddAsync(Arg.Do<ChoirJoinRequest>(row =>
        {
            row.Member = _member;
            row.Choir = _choir;
            _rows.Add(row);
        }), _ct).Returns(Task.CompletedTask);
        _requests.GetByIdAsync(Arg.Any<Guid>(), _ct).Returns(call => _rows.SingleOrDefault(x => x.Id == call.Arg<Guid>()));
        _requests.GetWithDetailsAsync(Arg.Any<Guid>(), _ct)
            .Returns(call => _rows.SingleOrDefault(x => x.Id == call.Arg<Guid>()));
        _requests.GetPendingForUpdateAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _ct)
            .Returns(call => _rows
                .Where(x => call.Arg<IReadOnlyCollection<Guid>>().Contains(x.MemberId)
                    && x.Status == ChoirJoinRequestStatus.Pending)
                .ToList());
        _memberships.ListAsync(Arg.Any<Expression<Func<ChoirMembership, bool>>>(), _ct)
            .Returns(call => _membershipRows.Where(call.Arg<Expression<Func<ChoirMembership, bool>>>().Compile()).ToList());
        _choirService.MoveMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), _ct).Returns(Result.Success());
        _notifications.SendAsync(Arg.Do<SendNotificationRequest>(_sent.Add), _ct).Returns(Task.CompletedTask);
    }

    private MemberProfile NewMember(string fullName)
    {
        var user = new User { Id = Guid.NewGuid(), FullName = fullName };
        var member = new MemberProfile { Id = Guid.NewGuid(), UserId = user.Id, User = user, Status = MemberStatus.Active };
        _members.GetByUserIdAsync(user.Id, _ct).Returns(member);
        return member;
    }

    private void SignInAsMember(MemberProfile member)
    {
        _currentUser.RoleName.Returns(RoleNames.ChoirMember);
        _currentUser.UserId.Returns(member.UserId);
    }

    private void SignInAsDirector(Guid userId)
    {
        _currentUser.RoleName.Returns(RoleNames.ChoirDirector);
        _currentUser.UserId.Returns(userId);
    }

    private ChoirJoinRequest AddRow(
        bool isInvitation, ChoirJoinRequestStatus status = ChoirJoinRequestStatus.Pending, Choir? choir = null)
    {
        choir ??= _choir;
        var row = new ChoirJoinRequest
        {
            Id = Guid.NewGuid(), MemberId = _member.Id, Member = _member, ChoirId = choir.Id, Choir = choir,
            IsInvitation = isInvitation, Status = status,
        };
        _rows.Add(row);
        return row;
    }

    // ---- Create ----

    [Fact]
    public async Task Create_MemberWithNoChoir_SavesPendingAndNotifiesDirector_Async()
    {
        SignInAsMember(_member);

        var result = await _sut.CreateAsync(new CreateChoirJoinRequestRequest { ChoirId = _choir.Id, Message = " hi " }, _ct);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(_rows);
        Assert.Equal((false, ChoirJoinRequestStatus.Pending, "hi"), (row.IsInvitation, row.Status, row.Message));
        await _requests.Received(1).SaveChangesAsync(_ct);
        var notice = Assert.Single(_sent);
        Assert.Equal([_directorId], notice.RecipientUserIds);
        Assert.Equal("Yêu cầu vào ca đoàn", notice.Title);
    }

    [Fact]
    public async Task Create_TransferFromAnotherChoir_SaysTransfer_Async()
    {
        SignInAsMember(_member);
        _member.ChoirMemberships = [new ChoirMembership { ChoirId = Guid.NewGuid(), Choir = new Choir { Name = "Old" }, IsActive = true }];

        await _sut.CreateAsync(new CreateChoirJoinRequestRequest { ChoirId = _choir.Id }, _ct);

        var notice = Assert.Single(_sent);
        Assert.Equal("Yêu cầu chuyển ca đoàn", notice.Title);
        Assert.Contains("Old", notice.Content);
    }

    [Fact]
    public async Task Create_ChoirWithoutDirector_NotifiesAdmins_Async()
    {
        SignInAsMember(_member);
        _choir.DirectorId = null;
        var adminId = Guid.NewGuid();
        _users.GetActiveUserIdsByRolesAsync(Arg.Is<IReadOnlyCollection<string>>(x => x.Contains(RoleNames.Admin)), _ct)
            .Returns([adminId]);

        await _sut.CreateAsync(new CreateChoirJoinRequestRequest { ChoirId = _choir.Id }, _ct);

        Assert.Equal([adminId], Assert.Single(_sent).RecipientUserIds);
    }

    [Fact]
    public async Task Create_OwnRequestAlreadyPending_ReturnsConflict_Async()
    {
        SignInAsMember(_member);
        AddRow(isInvitation: false, choir: new Choir { Id = Guid.NewGuid(), Name = "Other" });

        var result = await _sut.CreateAsync(new CreateChoirJoinRequestRequest { ChoirId = _choir.Id }, _ct);

        Assert.Equal(ErrorCodes.ChoirJoinRequestAlreadyPending, result.Code);
    }

    [Fact]
    public async Task Create_InvitedByThatChoir_ReturnsConflict_Async()
    {
        SignInAsMember(_member);
        AddRow(isInvitation: true);

        var result = await _sut.CreateAsync(new CreateChoirJoinRequestRequest { ChoirId = _choir.Id }, _ct);

        Assert.Equal(ErrorCodes.ChoirJoinRequestAlreadyPending, result.Code);
    }

    [Fact]
    public async Task Create_AlreadyInThatChoir_ReturnsAlreadyMember_Async()
    {
        SignInAsMember(_member);
        _membershipRows.Add(new ChoirMembership { MemberId = _member.Id, ChoirId = _choir.Id, IsActive = true });

        var result = await _sut.CreateAsync(new CreateChoirJoinRequestRequest { ChoirId = _choir.Id }, _ct);

        Assert.Equal(ErrorCodes.ChoirJoinRequestAlreadyMember, result.Code);
    }

    [Fact]
    public async Task Create_ChoirInactive_ReturnsInactive_Async()
    {
        SignInAsMember(_member);
        _choir.IsActive = false;

        var result = await _sut.CreateAsync(new CreateChoirJoinRequestRequest { ChoirId = _choir.Id }, _ct);

        Assert.Equal(ErrorCodes.ChoirInactive, result.Code);
    }

    [Fact]
    public async Task Create_ChoirMissing_ReturnsNotFound_Async()
    {
        SignInAsMember(_member);

        var result = await _sut.CreateAsync(new CreateChoirJoinRequestRequest { ChoirId = Guid.NewGuid() }, _ct);

        Assert.Equal(ErrorCodes.ChoirNotFound, result.Code);
    }

    // ---- Approve ----

    [Fact]
    public async Task Approve_RequestByItsDirector_MovesMemberCancelsOthersAndNotifiesMember_Async()
    {
        SignInAsDirector(_directorId);
        var row = AddRow(isInvitation: false);
        var otherInvitation = AddRow(isInvitation: true, choir: new Choir { Id = Guid.NewGuid(), Name = "Other" });

        var result = await _sut.ApproveAsync(row.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal((ChoirJoinRequestStatus.Approved, _directorId), (row.Status, row.ReviewedById));
        Assert.NotNull(row.ReviewedAt);
        Assert.Equal(ChoirJoinRequestStatus.Cancelled, otherInvitation.Status);
        await _choirService.Received(1).MoveMemberAsync(_member.Id, _choir.Id, _ct);
        Assert.Equal([_member.UserId], Assert.Single(_sent).RecipientUserIds);
    }

    [Fact]
    public async Task Approve_RequestByAdmin_IsAllowed_Async()
    {
        _currentUser.RoleName.Returns(RoleNames.Admin);
        _currentUser.UserId.Returns(Guid.NewGuid());
        var row = AddRow(isInvitation: false);

        var result = await _sut.ApproveAsync(row.Id, _ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Approve_RequestOfAnotherDirectorsChoir_ReturnsNotFound_Async()
    {
        SignInAsDirector(Guid.NewGuid());
        var row = AddRow(isInvitation: false);

        var result = await _sut.ApproveAsync(row.Id, _ct);

        Assert.Equal(ErrorCodes.ChoirJoinRequestNotFound, result.Code);
        await _choirService.DidNotReceiveWithAnyArgs().MoveMemberAsync(default, default, default);
    }

    [Fact]
    public async Task Approve_OwnRequestByTheMember_ReturnsNotFound_Async()
    {
        SignInAsMember(_member);
        var row = AddRow(isInvitation: false);

        var result = await _sut.ApproveAsync(row.Id, _ct);

        Assert.Equal(ErrorCodes.ChoirJoinRequestNotFound, result.Code);
    }

    [Fact]
    public async Task Approve_InvitationByTheMember_MovesMemberAndNotifiesDirector_Async()
    {
        SignInAsMember(_member);
        var row = AddRow(isInvitation: true);

        var result = await _sut.ApproveAsync(row.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChoirJoinRequestStatus.Approved, row.Status);
        Assert.Equal([_directorId], Assert.Single(_sent).RecipientUserIds);
    }

    [Fact]
    public async Task Approve_NotPending_ReturnsNotPending_Async()
    {
        SignInAsDirector(_directorId);
        var row = AddRow(isInvitation: false, status: ChoirJoinRequestStatus.Rejected);

        var result = await _sut.ApproveAsync(row.Id, _ct);

        Assert.Equal(ErrorCodes.ChoirJoinRequestNotPending, result.Code);
    }

    [Fact]
    public async Task Approve_Missing_ReturnsNotFound_Async()
    {
        SignInAsDirector(_directorId);

        var result = await _sut.ApproveAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.ChoirJoinRequestNotFound, result.Code);
    }

    [Fact]
    public async Task Approve_MoveFails_ReturnsItsCodeAndSendsNothing_Async()
    {
        SignInAsDirector(_directorId);
        var row = AddRow(isInvitation: false);
        _choirService.MoveMemberAsync(_member.Id, _choir.Id, _ct).Returns(Result.Failure(ErrorCodes.ChoirInactive));

        var result = await _sut.ApproveAsync(row.Id, _ct);

        Assert.Equal(ErrorCodes.ChoirInactive, result.Code);
        Assert.Empty(_sent);
    }

    // ---- Reject ----

    [Fact]
    public async Task Reject_RequestWithoutReason_ReturnsReasonRequired_Async()
    {
        SignInAsDirector(_directorId);
        var row = AddRow(isInvitation: false);

        var result = await _sut.RejectAsync(row.Id, new RejectChoirJoinRequestRequest { Reason = " " }, _ct);

        Assert.Equal(ErrorCodes.ChoirJoinRequestRejectReasonRequired, result.Code);
        Assert.Equal(ChoirJoinRequestStatus.Pending, row.Status);
    }

    [Fact]
    public async Task Reject_RequestWithReason_SavesAndTellsTheMemberWhy_Async()
    {
        SignInAsDirector(_directorId);
        var row = AddRow(isInvitation: false);

        var result = await _sut.RejectAsync(row.Id, new RejectChoirJoinRequestRequest { Reason = "Full" }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal((ChoirJoinRequestStatus.Rejected, "Full"), (row.Status, row.RejectReason));
        await _requests.Received(1).SaveChangesAsync(_ct);
        var notice = Assert.Single(_sent);
        Assert.Equal([_member.UserId], notice.RecipientUserIds);
        Assert.Contains("Full", notice.Content);
    }

    [Fact]
    public async Task Reject_InvitationByTheMember_NeedsNoReason_Async()
    {
        SignInAsMember(_member);
        var row = AddRow(isInvitation: true);

        var result = await _sut.RejectAsync(row.Id, new RejectChoirJoinRequestRequest(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChoirJoinRequestStatus.Rejected, row.Status);
        Assert.Equal([_directorId], Assert.Single(_sent).RecipientUserIds);
    }

    // ---- Cancel ----

    [Fact]
    public async Task Cancel_OwnRequestByTheMember_Cancels_Async()
    {
        SignInAsMember(_member);
        var row = AddRow(isInvitation: false);

        var result = await _sut.CancelAsync(row.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChoirJoinRequestStatus.Cancelled, row.Status);
    }

    [Fact]
    public async Task Cancel_MembersRequestByTheDirector_ReturnsNotFound_Async()
    {
        SignInAsDirector(_directorId);
        var row = AddRow(isInvitation: false);

        var result = await _sut.CancelAsync(row.Id, _ct);

        Assert.Equal(ErrorCodes.ChoirJoinRequestNotFound, result.Code);
        Assert.Equal(ChoirJoinRequestStatus.Pending, row.Status);
    }

    [Fact]
    public async Task Cancel_InvitationOfAnotherMember_ReturnsNotFound_Async()
    {
        SignInAsMember(NewMember("Someone else"));
        var row = AddRow(isInvitation: true);

        var result = await _sut.CancelAsync(row.Id, _ct);

        Assert.Equal(ErrorCodes.ChoirJoinRequestNotFound, result.Code);
    }

    // ---- Search ----

    [Fact]
    public async Task Search_AsMember_FiltersByTheirProfile_Async()
    {
        SignInAsMember(_member);
        _requests.SearchAsync(Arg.Any<SearchChoirJoinRequestsRequest>(), _member.Id, null, _ct)
            .Returns(new PagedList<ChoirJoinRequest>([], 1, 20, 0));

        var result = await _sut.SearchAsync(new SearchChoirJoinRequestsRequest(), _ct);

        Assert.True(result.IsSuccess);
        await _requests.Received(1).SearchAsync(Arg.Any<SearchChoirJoinRequestsRequest>(), _member.Id, null, _ct);
    }

    [Fact]
    public async Task Search_AsDirector_FiltersByTheirChoirs_Async()
    {
        SignInAsDirector(_directorId);
        _requests.SearchAsync(Arg.Any<SearchChoirJoinRequestsRequest>(), null, _directorId, _ct)
            .Returns(new PagedList<ChoirJoinRequest>([], 1, 20, 0));

        await _sut.SearchAsync(new SearchChoirJoinRequestsRequest(), _ct);

        await _requests.Received(1).SearchAsync(Arg.Any<SearchChoirJoinRequestsRequest>(), null, _directorId, _ct);
    }
}
