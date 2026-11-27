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

public class MemberSkillServiceTests
{
    private readonly IMemberSkillRepository _memberSkills = Substitute.For<IMemberSkillRepository>();
    private readonly IMemberProfileRepository _members = Substitute.For<IMemberProfileRepository>();
    private readonly IGenericRepository<ChoirMembership> _memberships = Substitute.For<IGenericRepository<ChoirMembership>>();
    private readonly IGenericRepository<Choir> _choirs = Substitute.For<IGenericRepository<Choir>>();
    private readonly IChoirJoinRequestRepository _joinRequests = Substitute.For<IChoirJoinRequestRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly List<ChoirMembership> _membershipRows = [];
    private readonly List<Choir> _choirRows = [];
    private readonly List<ChoirJoinRequest> _joinRequestRows = [];
    private readonly List<Guid> _allDirectorIds = [];
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IFileStorageService _files = Substitute.For<IFileStorageService>();
    private readonly IServiceRosterRepository _rosters = Substitute.For<IServiceRosterRepository>();
    private readonly IChoirScopeService _scope = ChoirScopeFakes.Unrestricted();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly MemberSkillService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly MemberProfile _member;
    private readonly Skill _tenor;

    public MemberSkillServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MemberSkillProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new MemberSkillService(
            _memberSkills, _members, _memberships, _choirs, _joinRequests, _users, _notifications, _files, _rosters, _scope, _email, mapper);
        _rosters.GetUpcomingAssignmentsForUpdateAsync(default, default, default, default).ReturnsForAnyArgs([]);
        _files.UploadAsync(default!, default!, default!, default, default).ReturnsForAnyArgs(call =>
            Result<FileUploadResponse>.Success(new FileUploadResponse(
                $"harmonia/{call.ArgAt<string>(2)}/new{Path.GetExtension(call.ArgAt<string>(1))}", string.Empty)));
        _files.GetSignedUrl(default!).ReturnsForAnyArgs(call => $"signed:{call.Arg<string>()}");

        _member = new MemberProfile
        {
            Id = Guid.NewGuid(), UserId = _userId, Status = MemberStatus.Active,
            User = new User { Id = _userId, FullName = "Anna" },
        };
        // The fakes evaluate the service's predicate against in-memory rows, as the database would.
        _memberships.ListAsync(Arg.Any<Expression<Func<ChoirMembership, bool>>>(), _ct)
            .Returns(call => _membershipRows.Where(call.Arg<Expression<Func<ChoirMembership, bool>>>().Compile()).ToList());
        _choirs.ListAsync(Arg.Any<Expression<Func<Choir, bool>>>(), _ct)
            .Returns(call => _choirRows.Where(call.Arg<Expression<Func<Choir, bool>>>().Compile()).ToList());
        _joinRequests.ListAsync(Arg.Any<Expression<Func<ChoirJoinRequest, bool>>>(), _ct)
            .Returns(call => _joinRequestRows.Where(call.Arg<Expression<Func<ChoirJoinRequest, bool>>>().Compile()).ToList());
        _users.GetActiveUserIdsByRolesAsync(
                Arg.Is<IReadOnlyCollection<string>>(x => x.Contains(RoleNames.ChoirDirector)), _ct)
            .Returns(_ => _allDirectorIds.ToList());
        var vocal = new SkillCategory { Id = SkillCategoryIds.Vocal, Name = "Vocal", IsActive = true };
        _tenor = new Skill { Id = Guid.NewGuid(), CategoryId = vocal.Id, Category = vocal, Name = "Tenor", IsActive = true };

        _members.GetByUserIdAsync(_userId, _ct).Returns(_member);
        _memberSkills.GetSkillWithCategoryAsync(_tenor.Id, _ct).Returns(_tenor);
        _memberSkills.TryAddAsync(Arg.Any<MemberSkill>(), _ct).Returns(true);
        _members.IsManagedByDirectorAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), _ct).Returns(true);
        _memberSkills.GetWithSkillAsync(Arg.Any<Guid>(), _ct).Returns(call => new MemberSkill
        {
            Id = call.Arg<Guid>(), MemberId = _member.Id, SkillId = _tenor.Id, Skill = _tenor,
            Status = ApprovalStatus.Pending, DeclaredAt = DateTime.UtcNow,
        });
    }

    private DeclareMemberSkillRequest NewRequest() => new() { SkillId = _tenor.Id };

    [Fact]
    public async Task Declare_NewSkill_AddsPendingRowForCaller_Async()
    {
        var result = await _sut.DeclareAsync(_userId, NewRequest(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApprovalStatus.Pending, result.Value!.Status);
        Assert.Equal("Tenor", result.Value.SkillName);
        Assert.Equal("Vocal", result.Value.CategoryName);
        await _memberSkills.Received(1).TryAddAsync(
            Arg.Is<MemberSkill>(x => x.MemberId == _member.Id && x.SkillId == _tenor.Id
                && x.Status == ApprovalStatus.Pending
                && x.ApprovedBy == null && x.RejectReason == null),
            _ct);
    }

    [Fact]
    public async Task Declare_OnlyRejectedBefore_AddsNewPendingRow_Async()
    {
        // HasActiveDeclarationAsync ignores rejected rows, so a past rejection reads as "no live declaration".
        _memberSkills.HasActiveDeclarationAsync(_member.Id, _tenor.Id, _ct).Returns(false);

        var result = await _sut.DeclareAsync(_userId, NewRequest(), _ct);

        Assert.True(result.IsSuccess);
        await _memberSkills.Received(1).TryAddAsync(Arg.Any<MemberSkill>(), _ct);
    }

    [Fact]
    public async Task Declare_PendingOrApprovedExists_ReturnsAlreadyDeclared_Async()
    {
        _memberSkills.HasActiveDeclarationAsync(_member.Id, _tenor.Id, _ct).Returns(true);

        var result = await _sut.DeclareAsync(_userId, NewRequest(), _ct);

        Assert.Equal(ErrorCodes.MemberSkillAlreadyDeclared, result.Code);
        await _memberSkills.DidNotReceive().TryAddAsync(Arg.Any<MemberSkill>(), _ct);
    }

    [Fact]
    public async Task Declare_ConcurrentDuplicateInsert_ReturnsAlreadyDeclared_Async()
    {
        _memberSkills.TryAddAsync(Arg.Any<MemberSkill>(), _ct).Returns(false);

        var result = await _sut.DeclareAsync(_userId, NewRequest(), _ct);

        Assert.Equal(ErrorCodes.MemberSkillAlreadyDeclared, result.Code);
    }

    [Fact]
    public async Task Declare_CallerHasNoProfile_ReturnsMemberNotFound_Async()
    {
        var result = await _sut.DeclareAsync(Guid.NewGuid(), NewRequest(), _ct);

        Assert.Equal(ErrorCodes.MemberNotFound, result.Code);
        await _memberSkills.DidNotReceive().TryAddAsync(Arg.Any<MemberSkill>(), _ct);
    }

    [Theory]
    [InlineData(MemberStatus.Inactive)]
    [InlineData(MemberStatus.Left)]
    public async Task Declare_MemberNotActive_ReturnsMemberNotActive_Async(MemberStatus status)
    {
        _member.Status = status;

        var result = await _sut.DeclareAsync(_userId, NewRequest(), _ct);

        Assert.Equal(ErrorCodes.MemberNotActive, result.Code);
    }

    [Fact]
    public async Task Declare_UnknownSkill_ReturnsSkillNotFound_Async()
    {
        var result = await _sut.DeclareAsync(_userId, new DeclareMemberSkillRequest { SkillId = Guid.NewGuid() }, _ct);

        Assert.Equal(ErrorCodes.SkillNotFound, result.Code);
    }

    [Fact]
    public async Task Declare_InactiveSkill_ReturnsSkillInactive_Async()
    {
        _tenor.IsActive = false;

        var result = await _sut.DeclareAsync(_userId, NewRequest(), _ct);

        Assert.Equal(ErrorCodes.SkillInactive, result.Code);
    }

    [Fact]
    public async Task Declare_SkillInInactiveCategory_ReturnsSkillInactive_Async()
    {
        _tenor.Category.IsActive = false;

        var result = await _sut.DeclareAsync(_userId, NewRequest(), _ct);

        Assert.Equal(ErrorCodes.SkillInactive, result.Code);
    }

    [Theory]
    [InlineData(Gender.Male, ErrorCodes.MemberSkillGenderMismatch)]
    [InlineData(null, ErrorCodes.MemberSkillGenderRequired)]
    public async Task Declare_GenderNotAllowed_ReturnsGenderError_Async(Gender? memberGender, string code)
    {
        _tenor.Gender = Gender.Female;
        _member.User = new User { Id = _userId, FullName = "Skill Member", Gender = memberGender };

        var result = await _sut.DeclareAsync(_userId, NewRequest(), _ct);

        Assert.Equal(code, result.Code);
        await _memberSkills.DidNotReceive().TryAddAsync(Arg.Any<MemberSkill>(), _ct);
    }

    [Fact]
    public async Task Declare_GenderMatches_AddsRow_Async()
    {
        _tenor.Gender = Gender.Male;
        _member.User = new User { Id = _userId, FullName = "Skill Member", Gender = Gender.Male };

        var result = await _sut.DeclareAsync(_userId, NewRequest(), _ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetMine_ReturnsCallersDeclarations_Async()
    {
        var paging = new PagingRequest();
        var rejected = new MemberSkill
        {
            Id = Guid.NewGuid(), MemberId = _member.Id, SkillId = _tenor.Id, Skill = _tenor,
            Status = ApprovalStatus.Rejected, RejectReason = "Not ready",
        };
        _memberSkills.GetByMemberAsync(_member.Id, ApprovalStatus.Rejected, paging, _ct)
            .Returns(new PagedList<MemberSkill>([rejected], 1, 20, 1));

        var result = await _sut.GetMineAsync(_userId, ApprovalStatus.Rejected, paging, _ct);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(ApprovalStatus.Rejected, item.Status);
        Assert.Equal("Not ready", item.RejectReason);
    }

    [Fact]
    public async Task GetMine_CallerHasNoProfile_ReturnsMemberNotFound_Async()
    {
        var result = await _sut.GetMineAsync(Guid.NewGuid(), null, new PagingRequest(), _ct);

        Assert.Equal(ErrorCodes.MemberNotFound, result.Code);
    }

    [Fact]
    public async Task GetMineById_OwnDeclaration_ReturnsIt_Async()
    {
        var id = Guid.NewGuid();

        var result = await _sut.GetMineByIdAsync(_userId, id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value!.Id);
        Assert.Equal("Tenor", result.Value.SkillName);
    }

    [Fact]
    public async Task GetMineById_AnotherMembersDeclaration_ReturnsMemberSkillNotFound_Async()
    {
        var id = Guid.NewGuid();
        _memberSkills.GetWithSkillAsync(id, _ct).Returns(new MemberSkill
        {
            Id = id, MemberId = Guid.NewGuid(), SkillId = _tenor.Id, Skill = _tenor,
        });

        var result = await _sut.GetMineByIdAsync(_userId, id, _ct);

        Assert.Equal(ErrorCodes.MemberSkillNotFound, result.Code);
    }

    [Fact]
    public async Task GetMineById_Missing_ReturnsMemberSkillNotFound_Async()
    {
        var id = Guid.NewGuid();
        _memberSkills.GetWithSkillAsync(id, _ct).Returns((MemberSkill?)null);

        var result = await _sut.GetMineByIdAsync(_userId, id, _ct);

        Assert.Equal(ErrorCodes.MemberSkillNotFound, result.Code);
    }

    private MemberSkill StubForReview(ApprovalStatus status)
    {
        _member.User = new User { Id = _userId, FullName = "Skill Member", Email = "member@example.com" };
        var row = new MemberSkill
        {
            Id = Guid.NewGuid(), MemberId = _member.Id, Member = _member, SkillId = _tenor.Id, Skill = _tenor,
            Status = status, DeclaredAt = DateTime.UtcNow,
        };
        _memberSkills.GetForReviewAsync(row.Id, _ct).Returns(row);
        _memberSkills.TrySaveReviewAsync(row, _ct).Returns(true);
        return row;
    }

    // ---- Who hears of a new declaration (D9) ----

    private Choir AddChoir(Guid? directorId)
    {
        var choir = new Choir { Id = Guid.NewGuid(), Name = "Choir", IsActive = true, DirectorId = directorId };
        _choirRows.Add(choir);
        return choir;
    }

    private async Task<SendNotificationRequest> DeclareAndGetNoticeAsync()
    {
        var sent = new List<SendNotificationRequest>();
        _notifications.SendAsync(Arg.Do<SendNotificationRequest>(sent.Add), _ct).Returns(Task.CompletedTask);

        Assert.True((await _sut.DeclareAsync(_userId, NewRequest(), _ct)).IsSuccess);

        var notice = Assert.Single(sent);
        Assert.Equal((NotificationType.SkillReview, nameof(MemberSkill)), (notice.Type, notice.ReferenceType));
        Assert.Contains("Anna", notice.Content);
        return notice;
    }

    [Fact]
    public async Task Declare_MemberInAChoir_NotifiesThatChoirsDirector_Async()
    {
        var directorId = Guid.NewGuid();
        _allDirectorIds.Add(Guid.NewGuid());
        _membershipRows.Add(new ChoirMembership { MemberId = _member.Id, ChoirId = AddChoir(directorId).Id, IsActive = true });
        // A pending request elsewhere does not matter once the member is in a choir.
        _joinRequestRows.Add(new ChoirJoinRequest
        {
            MemberId = _member.Id, ChoirId = AddChoir(Guid.NewGuid()).Id, Status = ChoirJoinRequestStatus.Pending,
        });

        var notice = await DeclareAndGetNoticeAsync();

        Assert.Equal([directorId], notice.RecipientUserIds);
    }

    [Fact]
    public async Task Declare_NoChoirButAskedToJoinOne_NotifiesThatChoirsDirector_Async()
    {
        var directorId = Guid.NewGuid();
        _allDirectorIds.Add(Guid.NewGuid());
        _membershipRows.Add(new ChoirMembership { MemberId = _member.Id, ChoirId = AddChoir(Guid.NewGuid()).Id, IsActive = false });
        _joinRequestRows.Add(new ChoirJoinRequest
        {
            MemberId = _member.Id, ChoirId = AddChoir(directorId).Id, Status = ChoirJoinRequestStatus.Pending,
        });

        var notice = await DeclareAndGetNoticeAsync();

        Assert.Equal([directorId], notice.RecipientUserIds);
    }

    [Fact]
    public async Task Declare_NoChoirAndOnlyAnInvitation_NotifiesEveryDirector_Async()
    {
        _allDirectorIds.AddRange([Guid.NewGuid(), Guid.NewGuid()]);
        _joinRequestRows.Add(new ChoirJoinRequest
        {
            MemberId = _member.Id, ChoirId = AddChoir(Guid.NewGuid()).Id, IsInvitation = true,
            Status = ChoirJoinRequestStatus.Pending,
        });

        var notice = await DeclareAndGetNoticeAsync();

        Assert.Equal(_allDirectorIds.Order(), notice.RecipientUserIds.Order());
    }

    [Fact]
    public async Task Declare_ChoirWithoutDirector_FallsBackToEveryDirector_Async()
    {
        _allDirectorIds.Add(Guid.NewGuid());
        _membershipRows.Add(new ChoirMembership { MemberId = _member.Id, ChoirId = AddChoir(null).Id, IsActive = true });

        var notice = await DeclareAndGetNoticeAsync();

        Assert.Equal(_allDirectorIds, notice.RecipientUserIds);
    }

    [Fact]
    public async Task Declare_Fails_NotifiesNobody_Async()
    {
        _allDirectorIds.Add(Guid.NewGuid());
        _memberSkills.HasActiveDeclarationAsync(_member.Id, _tenor.Id, _ct).Returns(true);

        var result = await _sut.DeclareAsync(_userId, NewRequest(), _ct);

        Assert.Equal(ErrorCodes.MemberSkillAlreadyDeclared, result.Code);
        await _notifications.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    // ---- Bulk approve ----

    [Fact]
    public async Task ApproveMany_ApprovesEachPendingRow_AndReportsTheOthersWithTheirCode_Async()
    {
        var pending = StubForReview(ApprovalStatus.Pending);
        var reviewed = StubForReview(ApprovalStatus.Rejected);
        var missing = Guid.NewGuid();

        var result = await _sut.ApproveManyAsync(
            Guid.NewGuid(), new ApproveMemberSkillsRequest { Ids = [pending.Id, reviewed.Id, missing, pending.Id] }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal([pending.Id], result.Value!.Approved.Select(x => x.Id));
        Assert.Equal(ApprovalStatus.Approved, pending.Status);
        Assert.Equal(
            [(reviewed.Id, ErrorCodes.MemberSkillAlreadyReviewed), (missing, ErrorCodes.MemberSkillNotFound)],
            result.Value.Failed.Select(x => (x.Id, x.Code)));
        await _memberSkills.Received(1).TrySaveReviewAsync(pending, _ct);
    }

    // ---- Assign ----

    private void StubAssignable()
    {
        _members.GetWithUserAsync(_member.Id, _ct).Returns(_member);
        _memberSkills.GetForReviewAsync(Arg.Any<Guid>(), _ct).Returns(call => new MemberSkill
        {
            Id = call.Arg<Guid>(), MemberId = _member.Id, Member = _member, SkillId = _tenor.Id, Skill = _tenor,
            Status = ApprovalStatus.Approved, DeclaredAt = DateTime.UtcNow,
        });
    }

    [Fact]
    public async Task Assign_ManagedMember_AddsApprovedRowAndNotifiesMember_Async()
    {
        StubAssignable();
        var directorId = Guid.NewGuid();

        var result = await _sut.AssignAsync(
            directorId, new AssignMemberSkillRequest { MemberId = _member.Id, SkillId = _tenor.Id }, _ct);

        Assert.True(result.IsSuccess);
        await _memberSkills.Received(1).TryAddAsync(
            Arg.Is<MemberSkill>(x => x.MemberId == _member.Id && x.SkillId == _tenor.Id
                && x.Status == ApprovalStatus.Approved && x.ApprovedBy == directorId && x.ApprovedAt != null),
            _ct);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(x => x.Type == NotificationType.SkillReview && x.RecipientUserIds.Single() == _userId),
            _ct);
    }

    [Fact]
    public async Task Assign_MemberOutsideTheDirectorsScope_ReturnsMemberNotFound_Async()
    {
        StubAssignable();
        _members.IsManagedByDirectorAsync(_member.Id, Arg.Any<Guid>(), _ct).Returns(false);

        var result = await _sut.AssignAsync(
            Guid.NewGuid(), new AssignMemberSkillRequest { MemberId = _member.Id, SkillId = _tenor.Id }, _ct);

        Assert.Equal(ErrorCodes.MemberNotFound, result.Code);
        await _memberSkills.DidNotReceiveWithAnyArgs().TryAddAsync(default!, default);
    }

    [Fact]
    public async Task Assign_UnknownMember_ReturnsMemberNotFound_Async()
    {
        var result = await _sut.AssignAsync(
            Guid.NewGuid(), new AssignMemberSkillRequest { MemberId = Guid.NewGuid(), SkillId = _tenor.Id }, _ct);

        Assert.Equal(ErrorCodes.MemberNotFound, result.Code);
    }

    [Fact]
    public async Task Assign_AlreadyPendingOrApproved_ReturnsAlreadyDeclared_Async()
    {
        StubAssignable();
        _memberSkills.HasActiveDeclarationAsync(_member.Id, _tenor.Id, _ct).Returns(true);

        var result = await _sut.AssignAsync(
            Guid.NewGuid(), new AssignMemberSkillRequest { MemberId = _member.Id, SkillId = _tenor.Id }, _ct);

        Assert.Equal(ErrorCodes.MemberSkillAlreadyDeclared, result.Code);
    }

    [Fact]
    public async Task Assign_GenderMismatch_ReturnsMismatch_Async()
    {
        StubAssignable();
        _tenor.Gender = Gender.Male;
        _member.User.Gender = Gender.Female;

        var result = await _sut.AssignAsync(
            Guid.NewGuid(), new AssignMemberSkillRequest { MemberId = _member.Id, SkillId = _tenor.Id }, _ct);

        Assert.Equal(ErrorCodes.MemberSkillGenderMismatch, result.Code);
    }

    [Fact]
    public async Task Approve_Pending_SetsApprovedAndNotifiesMember_Async()
    {
        var row = StubForReview(ApprovalStatus.Pending);
        var directorId = Guid.NewGuid();

        var result = await _sut.ApproveAsync(directorId, row.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApprovalStatus.Approved, result.Value!.Status);
        Assert.Equal("Skill Member", result.Value.MemberFullName);
        Assert.Equal(directorId, row.ApprovedBy);
        Assert.NotNull(row.ApprovedAt);
        await _memberSkills.Received(1).TrySaveReviewAsync(row, _ct);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(x => x.Type == NotificationType.SkillReview
                && x.RecipientUserIds.Single() == _userId && x.ReferenceId == row.Id),
            _ct);
        await _email.Received(1).SendAsync(
            "member@example.com", Arg.Any<string>(), Arg.Is<string>(x => x.Contains("Tenor") && x.Contains("đã được")), _ct);
    }

    [Fact]
    public async Task Reject_Pending_StoresReasonAndNotifiesMember_Async()
    {
        var row = StubForReview(ApprovalStatus.Pending);

        var result = await _sut.RejectAsync(Guid.NewGuid(), row.Id, new RejectMemberSkillRequest { Reason = " Not ready " }, _ct);

        Assert.Equal(ApprovalStatus.Rejected, result.Value!.Status);
        Assert.Equal("Not ready", result.Value.RejectReason);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(x => x.Content.Contains("Not ready")), _ct);
        await _email.Received(1).SendAsync(
            "member@example.com", Arg.Any<string>(), Arg.Is<string>(x => x.Contains("Lý do: Not ready")), _ct);
    }

    [Theory]
    [InlineData(ApprovalStatus.Approved)]
    [InlineData(ApprovalStatus.Rejected)]
    public async Task Approve_AlreadyReviewed_ReturnsAlreadyReviewed_Async(ApprovalStatus status)
    {
        var row = StubForReview(status);

        var result = await _sut.ApproveAsync(Guid.NewGuid(), row.Id, _ct);

        Assert.Equal(ErrorCodes.MemberSkillAlreadyReviewed, result.Code);
        await _memberSkills.DidNotReceive().TrySaveReviewAsync(Arg.Any<MemberSkill>(), _ct);
        await _notifications.DidNotReceive().SendAsync(Arg.Any<SendNotificationRequest>(), _ct);
        await _email.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
    }

    [Theory]
    [InlineData(ApprovalStatus.Approved)]
    [InlineData(ApprovalStatus.Rejected)]
    public async Task Reject_AlreadyReviewed_ReturnsAlreadyReviewed_Async(ApprovalStatus status)
    {
        var row = StubForReview(status);

        var result = await _sut.RejectAsync(Guid.NewGuid(), row.Id, new RejectMemberSkillRequest { Reason = "Not ready" }, _ct);

        Assert.Equal(ErrorCodes.MemberSkillAlreadyReviewed, result.Code);
        await _memberSkills.DidNotReceive().TrySaveReviewAsync(Arg.Any<MemberSkill>(), _ct);
        await _notifications.DidNotReceive().SendAsync(Arg.Any<SendNotificationRequest>(), _ct);
    }

    [Fact]
    public async Task Revoke_Approved_DropsUpcomingLines_ReopensRoster_AndNotifiesMemberAndLeadDirector_Async()
    {
        var row = StubForReview(ApprovalStatus.Approved);
        var revokerId = Guid.NewGuid();
        var leadDirectorId = Guid.NewGuid();
        var liturgicalEvent = new LiturgicalEvent { Id = Guid.NewGuid(), EventDate = VietnamTime.Today.AddDays(5) };
        var roster = new ServiceRoster
        {
            Id = Guid.NewGuid(), Status = RosterStatus.Finalized, FinalizedAt = DateTime.UtcNow,
            WholeServiceFinalizedAt = DateTime.UtcNow, LiturgicalEvent = liturgicalEvent,
        };
        var line = new RosterAssignment { Id = Guid.NewGuid(), MemberId = row.MemberId, SkillId = row.SkillId, Roster = roster };
        roster.Assignments.Add(line);
        _rosters.GetUpcomingAssignmentsForUpdateAsync(row.MemberId, row.SkillId, Arg.Any<DateOnly>(), _ct).Returns([line]);
        // The revoker stands in as substitute here, so only the choir's own director hears of the gap.
        _scope.GetLeadDirectorUserIdsAsync(liturgicalEvent.Id, _ct).Returns([leadDirectorId, revokerId]);

        var result = await _sut.RevokeAsync(revokerId, row.Id, new RevokeMemberSkillRequest { Reason = " Lost voice " }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApprovalStatus.Revoked, result.Value!.Status);
        Assert.Equal((revokerId, "Lost voice"), (row.RevokedBy, row.RevokeReason));
        Assert.NotNull(row.RevokedAt);
        Assert.Empty(roster.Assignments);
        Assert.Equal((RosterStatus.Draft, null, null), (roster.Status, roster.FinalizedAt, roster.WholeServiceFinalizedAt));
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(x => x.Type == NotificationType.SkillReview && x.RecipientUserIds.Single() == _userId), _ct);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(x => x.Type == NotificationType.AssignmentNotice
                && x.RecipientUserIds.Single() == leadDirectorId), _ct);
        await _email.Received(1).SendAsync(
            "member@example.com", Arg.Any<string>(), Arg.Is<string>(x => x.Contains("Lý do: Lost voice")), _ct);
    }

    [Theory]
    [InlineData(ApprovalStatus.Pending)]
    [InlineData(ApprovalStatus.Rejected)]
    [InlineData(ApprovalStatus.Revoked)]
    public async Task Revoke_NotApproved_ReturnsNotApproved_AndSavesNothing_Async(ApprovalStatus status)
    {
        var row = StubForReview(status);

        var result = await _sut.RevokeAsync(Guid.NewGuid(), row.Id, new RevokeMemberSkillRequest { Reason = "x" }, _ct);

        Assert.Equal(ErrorCodes.MemberSkillNotApproved, result.Code);
        await _memberSkills.DidNotReceive().TrySaveReviewAsync(Arg.Any<MemberSkill>(), _ct);
    }

    [Fact]
    public async Task Revoke_MemberOfAnotherDirectorsChoir_ReturnsNotFound_Async()
    {
        var row = StubForReview(ApprovalStatus.Approved);
        var directorId = Guid.NewGuid();
        _members.IsManagedByDirectorAsync(row.MemberId, directorId, _ct).Returns(false);

        var result = await _sut.RevokeAsync(directorId, row.Id, new RevokeMemberSkillRequest { Reason = "x" }, _ct);

        Assert.Equal(ErrorCodes.MemberSkillNotFound, result.Code);
        Assert.Equal(ApprovalStatus.Approved, row.Status);
    }

    [Fact]
    public async Task Approve_MemberOfAnotherDirectorsChoir_ReturnsNotFound_AndSavesNothing_Async()
    {
        var row = StubForReview(ApprovalStatus.Pending);
        var directorId = Guid.NewGuid();
        _members.IsManagedByDirectorAsync(row.MemberId, directorId, _ct).Returns(false);

        var result = await _sut.ApproveAsync(directorId, row.Id, _ct);

        Assert.Equal(ErrorCodes.MemberSkillNotFound, result.Code);
        Assert.Equal(ApprovalStatus.Pending, row.Status);
        await _memberSkills.DidNotReceive().TrySaveReviewAsync(Arg.Any<MemberSkill>(), _ct);
    }

    [Fact]
    public async Task Approve_ConcurrentReview_ReturnsAlreadyReviewed_AndDoesNotNotify_Async()
    {
        var row = StubForReview(ApprovalStatus.Pending);
        _memberSkills.TrySaveReviewAsync(row, _ct).Returns(false);

        var result = await _sut.ApproveAsync(Guid.NewGuid(), row.Id, _ct);

        Assert.Equal(ErrorCodes.MemberSkillAlreadyReviewed, result.Code);
        await _notifications.DidNotReceive().SendAsync(Arg.Any<SendNotificationRequest>(), _ct);
    }

    [Fact]
    public async Task Approve_Missing_ReturnsMemberSkillNotFound_Async()
    {
        var result = await _sut.ApproveAsync(Guid.NewGuid(), Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.MemberSkillNotFound, result.Code);
    }

    private static FileContent Evidence(string name, long length = 1024) => new(Stream.Null, name, length);

    [Theory]
    [InlineData("take.mp3", MemberSkillService.MaxVoiceSizeBytes, "Voice")]
    [InlineData("TAKE.M4A", 1024L, "Voice")]
    [InlineData("take.wav", 1024L, "Voice")]
    [InlineData("clip.mp4", MemberSkillService.MaxClipSizeBytes, "Clip")]
    [InlineData("clip.mov", 1024L, "Clip")]
    public async Task UploadEvidence_AllowedFile_StoresPrivatelyAndReturnsSignedUrl_Async(string name, long length, string type)
    {
        var row = StubForReview(ApprovalStatus.Pending);

        var result = await _sut.UploadEvidenceAsync(_userId, row.Id, Evidence(name, length), _ct);

        Assert.True(result.IsSuccess);
        var evidence = result.Value!.Evidence!;
        Assert.Equal((type, name, length), (evidence.Type, evidence.FileName, evidence.FileSizeBytes));
        Assert.Equal($"signed:{row.EvidencePublicId}", evidence.Url);
        await _files.Received(1).UploadAsync(Stream.Null, name, "member-skill-evidence", true, _ct);
    }

    [Theory]
    [InlineData("take.exe", 1024L, ErrorCodes.MemberSkillEvidenceTypeNotAllowed)]
    [InlineData("noextension", 1024L, ErrorCodes.MemberSkillEvidenceTypeNotAllowed)]
    [InlineData("take.mp3", 0L, ErrorCodes.MemberSkillEvidenceRequired)]
    [InlineData("take.mp3", MemberSkillService.MaxVoiceSizeBytes + 1, ErrorCodes.MemberSkillEvidenceTooLarge)]
    [InlineData("clip.mp4", MemberSkillService.MaxClipSizeBytes + 1, ErrorCodes.MemberSkillEvidenceTooLarge)]
    public async Task UploadEvidence_BadFile_ReturnsCode_AndUploadsNothing_Async(string name, long length, string code)
    {
        var row = StubForReview(ApprovalStatus.Pending);

        var result = await _sut.UploadEvidenceAsync(_userId, row.Id, Evidence(name, length), _ct);

        Assert.Equal(code, result.Code);
        await _files.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task UploadEvidence_NoFile_ReturnsRequired_Async()
    {
        var row = StubForReview(ApprovalStatus.Pending);

        var result = await _sut.UploadEvidenceAsync(_userId, row.Id, null, _ct);

        Assert.Equal(ErrorCodes.MemberSkillEvidenceRequired, result.Code);
    }

    [Fact]
    public async Task UploadEvidence_AnotherMembersDeclaration_ReturnsNotFound_AndUploadsNothing_Async()
    {
        var row = StubForReview(ApprovalStatus.Pending);
        row.MemberId = Guid.NewGuid();

        var result = await _sut.UploadEvidenceAsync(_userId, row.Id, Evidence("take.mp3"), _ct);

        Assert.Equal(ErrorCodes.MemberSkillNotFound, result.Code);
        await _files.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, default, default);
    }

    [Theory]
    [InlineData(ApprovalStatus.Approved)]
    [InlineData(ApprovalStatus.Rejected)]
    public async Task UploadEvidence_AlreadyReviewed_ReturnsAlreadyReviewed_Async(ApprovalStatus status)
    {
        var row = StubForReview(status);

        var result = await _sut.UploadEvidenceAsync(_userId, row.Id, Evidence("take.mp3"), _ct);

        Assert.Equal(ErrorCodes.MemberSkillAlreadyReviewed, result.Code);
        await _files.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task UploadEvidence_MemberNotActive_ReturnsMemberNotActive_Async()
    {
        var row = StubForReview(ApprovalStatus.Pending);
        _member.Status = MemberStatus.Left;

        var result = await _sut.UploadEvidenceAsync(_userId, row.Id, Evidence("take.mp3"), _ct);

        Assert.Equal(ErrorCodes.MemberNotActive, result.Code);
    }

    [Fact]
    public async Task UploadEvidence_Replacing_DeletesPreviousFile_Async()
    {
        var row = StubForReview(ApprovalStatus.Pending);
        row.EvidencePublicId = "harmonia/member-skill-evidence/old.mp3";

        var result = await _sut.UploadEvidenceAsync(_userId, row.Id, Evidence("clip.mp4"), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("harmonia/member-skill-evidence/new.mp4", row.EvidencePublicId);
        await _files.Received(1).DeleteAsync("harmonia/member-skill-evidence/old.mp3", true, CancellationToken.None);
    }

    [Fact]
    public async Task UploadEvidence_DirectorReviewsDuringUpload_RemovesNewFile_AndKeepsPrevious_Async()
    {
        var row = StubForReview(ApprovalStatus.Pending);
        row.EvidencePublicId = "harmonia/member-skill-evidence/old.mp3";
        _memberSkills.TrySaveReviewAsync(row, _ct).Returns(false);

        var result = await _sut.UploadEvidenceAsync(_userId, row.Id, Evidence("clip.mp4"), _ct);

        Assert.Equal(ErrorCodes.MemberSkillAlreadyReviewed, result.Code);
        await _files.Received(1).DeleteAsync("harmonia/member-skill-evidence/new.mp4", true, CancellationToken.None);
        await _files.DidNotReceive().DeleteAsync("harmonia/member-skill-evidence/old.mp3", Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadEvidence_SaveThrows_RemovesNewFile_AndRethrows_Async()
    {
        var row = StubForReview(ApprovalStatus.Pending);
        _memberSkills.TrySaveReviewAsync(row, _ct).Returns<bool>(_ => throw new InvalidOperationException("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UploadEvidenceAsync(_userId, row.Id, Evidence("take.mp3"), _ct));

        await _files.Received(1).DeleteAsync("harmonia/member-skill-evidence/new.mp3", true, CancellationToken.None);
    }

    [Fact]
    public async Task UploadEvidence_StorageFails_ReturnsCode_AndSavesNothing_Async()
    {
        var row = StubForReview(ApprovalStatus.Pending);
        _files.UploadAsync(default!, default!, default!, default, default)
            .ReturnsForAnyArgs(Result<FileUploadResponse>.Failure(ErrorCodes.ExternalStorageFailed));

        var result = await _sut.UploadEvidenceAsync(_userId, row.Id, Evidence("take.mp3"), _ct);

        Assert.Equal(ErrorCodes.ExternalStorageFailed, result.Code);
        Assert.Null(row.EvidencePublicId);
        await _memberSkills.DidNotReceive().TrySaveReviewAsync(Arg.Any<MemberSkill>(), _ct);
    }

    [Fact]
    public async Task GetPending_RowWithEvidence_ReturnsSignedUrl_RowWithoutEvidence_ReturnsNull_Async()
    {
        var paging = new PagingRequest();
        var directorId = Guid.NewGuid();
        var withEvidence = StubForReview(ApprovalStatus.Pending);
        withEvidence.EvidencePublicId = "harmonia/member-skill-evidence/a.mov";
        withEvidence.EvidenceFileName = "a.mov";
        withEvidence.EvidenceSizeBytes = 2048;
        withEvidence.EvidenceUploadedAt = DateTime.UtcNow;
        var without = StubForReview(ApprovalStatus.Pending);
        _memberSkills.GetPendingAsync(directorId, paging, _ct)
            .Returns(new PagedList<MemberSkill>([withEvidence, without], 1, 20, 2));

        var result = await _sut.GetPendingAsync(directorId, paging, _ct);

        Assert.Equal("Clip", result.Value!.Items[0].Evidence!.Type);
        Assert.Equal("signed:harmonia/member-skill-evidence/a.mov", result.Value.Items[0].Evidence!.Url);
        Assert.Null(result.Value.Items[1].Evidence);
    }
}
