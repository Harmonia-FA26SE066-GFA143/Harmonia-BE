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
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly MemberSkillService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly MemberProfile _member;
    private readonly Skill _tenor;

    public MemberSkillServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MemberSkillProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new MemberSkillService(_memberSkills, _members, _notifications, mapper);

        _member = new MemberProfile { Id = Guid.NewGuid(), UserId = _userId, Status = MemberStatus.Active };
        var vocal = new SkillCategory { Id = SkillCategoryIds.Vocal, Name = "Vocal", IsActive = true };
        _tenor = new Skill { Id = Guid.NewGuid(), CategoryId = vocal.Id, Category = vocal, Name = "Tenor", IsActive = true };

        _members.GetByUserIdAsync(_userId, _ct).Returns(_member);
        _memberSkills.GetSkillWithCategoryAsync(_tenor.Id, _ct).Returns(_tenor);
        _memberSkills.TryAddAsync(Arg.Any<MemberSkill>(), _ct).Returns(true);
        _memberSkills.GetWithSkillAsync(Arg.Any<Guid>(), _ct).Returns(call => new MemberSkill
        {
            Id = call.Arg<Guid>(), MemberId = _member.Id, SkillId = _tenor.Id, Skill = _tenor,
            Level = SkillLevel.Intermediate, Status = ApprovalStatus.Pending, DeclaredAt = DateTime.UtcNow,
        });
    }

    private DeclareMemberSkillRequest NewRequest() => new() { SkillId = _tenor.Id, Level = SkillLevel.Intermediate };

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
                && x.Status == ApprovalStatus.Pending && x.Level == SkillLevel.Intermediate
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
        _member.User = new User { Id = _userId, FullName = "Skill Member" };
        var row = new MemberSkill
        {
            Id = Guid.NewGuid(), MemberId = _member.Id, Member = _member, SkillId = _tenor.Id, Skill = _tenor,
            Status = status, DeclaredAt = DateTime.UtcNow,
        };
        _memberSkills.GetForReviewAsync(row.Id, _ct).Returns(row);
        return row;
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
        await _memberSkills.Received(1).SaveChangesAsync(_ct);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(x => x.Type == NotificationType.SkillReview
                && x.RecipientUserIds.Single() == _userId && x.ReferenceId == row.Id),
            _ct);
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
    }

    [Theory]
    [InlineData(ApprovalStatus.Approved)]
    [InlineData(ApprovalStatus.Rejected)]
    public async Task Approve_AlreadyReviewed_ReturnsAlreadyReviewed_Async(ApprovalStatus status)
    {
        var row = StubForReview(status);

        var result = await _sut.ApproveAsync(Guid.NewGuid(), row.Id, _ct);

        Assert.Equal(ErrorCodes.MemberSkillAlreadyReviewed, result.Code);
        await _memberSkills.DidNotReceive().SaveChangesAsync(_ct);
        await _notifications.DidNotReceive().SendAsync(Arg.Any<SendNotificationRequest>(), _ct);
    }

    [Fact]
    public async Task Approve_Missing_ReturnsMemberSkillNotFound_Async()
    {
        var result = await _sut.ApproveAsync(Guid.NewGuid(), Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.MemberSkillNotFound, result.Code);
    }
}
