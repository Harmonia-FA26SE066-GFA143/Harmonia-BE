using System.Linq.Expressions;
using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Mappings;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class ChoirServiceTests
{
    private readonly IGenericRepository<Choir> _choirs = Substitute.For<IGenericRepository<Choir>>();
    private readonly IGenericRepository<ChoirMembership> _memberships = Substitute.For<IGenericRepository<ChoirMembership>>();
    private readonly IGenericRepository<LiturgicalEventChoir> _eventChoirs = Substitute.For<IGenericRepository<LiturgicalEventChoir>>();
    private readonly IMemberProfileRepository _members = Substitute.For<IMemberProfileRepository>();
    private readonly ILiturgicalEventRepository _events = Substitute.For<ILiturgicalEventRepository>();
    private readonly ChoirService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Choir _choir;
    private readonly LiturgicalEvent _event;
    private readonly List<Choir> _choirRows = [];
    private readonly List<MemberProfile> _memberRows = [];
    private readonly List<ChoirMembership> _membershipRows = [];
    private readonly List<ChoirMembership> _addedMemberships = [];
    private readonly List<LiturgicalEventChoir> _eventChoirRows = [];
    private readonly List<LiturgicalEventChoir> _addedEventChoirs = [];
    private readonly List<LiturgicalEventChoir> _removedEventChoirs = [];

    public ChoirServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<ChoirProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new ChoirService(_choirs, _memberships, _eventChoirs, _members, _events, mapper);

        // The fakes evaluate the service's predicate against in-memory rows, as the database would.
        _choirs.ListAsync(Arg.Any<Expression<Func<Choir, bool>>>(), _ct)
            .Returns(call => _choirRows.Where(call.Arg<Expression<Func<Choir, bool>>>().Compile()).ToList());
        _choirs.GetByIdAsync(Arg.Any<Guid>(), _ct)
            .Returns(call => _choirRows.SingleOrDefault(x => x.Id == call.Arg<Guid>()));
        _members.ListAsync(Arg.Any<Expression<Func<MemberProfile, bool>>>(), _ct)
            .Returns(call => _memberRows.Where(call.Arg<Expression<Func<MemberProfile, bool>>>().Compile()).ToList());
        _members.GetByIdsWithUserAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _ct)
            .Returns(call => _memberRows.Where(x => call.Arg<IReadOnlyCollection<Guid>>().Contains(x.Id)).ToList());
        _memberships.ListAsync(Arg.Any<Expression<Func<ChoirMembership, bool>>>(), _ct)
            .Returns(call => _membershipRows.Where(call.Arg<Expression<Func<ChoirMembership, bool>>>().Compile()).ToList());
        _memberships.GetByIdAsync(Arg.Any<Guid>(), _ct)
            .Returns(call => _membershipRows.SingleOrDefault(x => x.Id == call.Arg<Guid>()));
        _memberships.AddAsync(Arg.Do<ChoirMembership>(_addedMemberships.Add), _ct).Returns(Task.CompletedTask);
        _eventChoirs.ListAsync(Arg.Any<Expression<Func<LiturgicalEventChoir, bool>>>(), _ct)
            .Returns(call => _eventChoirRows.Where(call.Arg<Expression<Func<LiturgicalEventChoir, bool>>>().Compile()).ToList());
        _eventChoirs.AddAsync(Arg.Do<LiturgicalEventChoir>(_addedEventChoirs.Add), _ct).Returns(Task.CompletedTask);
        _eventChoirs.When(x => x.Remove(Arg.Any<LiturgicalEventChoir>()))
            .Do(call => _removedEventChoirs.Add(call.Arg<LiturgicalEventChoir>()));

        _choir = NewChoir("Cecilia Choir");
        _event = new LiturgicalEvent
        {
            Id = Guid.NewGuid(), EventDate = VietnamTime.Today.AddDays(7), Status = EventStatus.Published,
        };
        _events.GetByIdAsync(_event.Id, _ct).Returns(_event);
    }

    private Choir NewChoir(string name, bool isActive = true)
    {
        var choir = new Choir { Id = Guid.NewGuid(), Name = name, IsActive = isActive };
        _choirRows.Add(choir);
        return choir;
    }

    private MemberProfile NewMember(MemberStatus status = MemberStatus.Active, string fullName = "Member")
    {
        var id = Guid.NewGuid();
        var member = new MemberProfile
        {
            Id = id, Status = status,
            User = new User { Id = Guid.NewGuid(), FullName = fullName, Email = $"{id}@example.com" },
        };
        member.UserId = member.User.Id;
        _memberRows.Add(member);
        return member;
    }

    private ChoirMembership NewMembership(MemberProfile member, bool isActive = true)
    {
        var row = new ChoirMembership
        {
            Id = Guid.NewGuid(), ChoirId = _choir.Id, MemberId = member.Id,
            JoinedDate = new DateOnly(2025, 1, 1), IsActive = isActive,
        };
        _membershipRows.Add(row);
        return row;
    }

    private LiturgicalEventChoir AssignToEvent(Choir choir)
    {
        var row = new LiturgicalEventChoir { Id = Guid.NewGuid(), EventId = _event.Id, ChoirId = choir.Id };
        _eventChoirRows.Add(row);
        return row;
    }

    [Fact]
    public async Task Create_TrimsAndSaves_Async()
    {
        var result = await _sut.CreateAsync(new CreateChoirRequest { Name = "  Cecilia  ", Description = " " }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cecilia", result.Value!.Name);
        Assert.Null(result.Value.Description);
        Assert.True(result.Value.IsActive);
        await _choirs.Received(1).AddAsync(Arg.Is<Choir>(c => c.Name == "Cecilia"), _ct);
        await _choirs.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsConflict_Async()
    {
        var result = await _sut.CreateAsync(new CreateChoirRequest { Name = _choir.Name }, _ct);

        Assert.Equal(ErrorCodes.ChoirNameAlreadyExists, result.Code);
        await _choirs.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Update_KeepingOwnName_RenamesDescriptionAndDeactivates_Async()
    {
        var result = await _sut.UpdateAsync(
            _choir.Id, new UpdateChoirRequest { Name = _choir.Name, Description = " Youth ", IsActive = false }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Youth", _choir.Description);
        Assert.False(_choir.IsActive);
        await _choirs.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Update_NameOfAnotherChoir_ReturnsConflict_Async()
    {
        NewChoir("Joseph Choir");

        var result = await _sut.UpdateAsync(
            _choir.Id, new UpdateChoirRequest { Name = "Joseph Choir", IsActive = true }, _ct);

        Assert.Equal(ErrorCodes.ChoirNameAlreadyExists, result.Code);
        Assert.Equal("Cecilia Choir", _choir.Name);
    }

    [Fact]
    public async Task Update_ChoirMissing_ReturnsNotFound_Async()
    {
        var result = await _sut.UpdateAsync(Guid.NewGuid(), new UpdateChoirRequest { Name = "X", IsActive = true }, _ct);

        Assert.Equal(ErrorCodes.ChoirNotFound, result.Code);
    }

    [Fact]
    public async Task GetMembers_ReturnsActiveMembersOrderedByName_Async()
    {
        var bao = NewMember(fullName: "Bao");
        var an = NewMember(fullName: "An");
        var left = NewMember(fullName: "Chi");
        NewMembership(bao);
        NewMembership(an);
        NewMembership(left, isActive: false);

        var result = await _sut.GetMembersAsync(_choir.Id, _ct);

        Assert.True(result.IsSuccess);
        var members = result.Value!;
        Assert.Equal(["An", "Bao"], members.Select(x => x.FullName));
        Assert.Equal(an.Id, members[0].MemberId);
        Assert.Equal(an.UserId, members[0].UserId);
    }

    [Fact]
    public async Task GetMembers_ChoirMissing_ReturnsNotFound_Async()
    {
        var result = await _sut.GetMembersAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.ChoirNotFound, result.Code);
    }

    [Fact]
    public async Task AddMembers_ChoirMissing_ReturnsNotFound_Async()
    {
        var member = NewMember();

        var result = await _sut.AddMembersAsync(Guid.NewGuid(), new AddChoirMembersRequest { MemberIds = [member.Id] }, _ct);

        Assert.Equal(ErrorCodes.ChoirNotFound, result.Code);
    }

    [Fact]
    public async Task AddMembers_ChoirInactive_ReturnsInactive_Async()
    {
        var inactive = NewChoir("Old Choir", isActive: false);
        var member = NewMember();

        var result = await _sut.AddMembersAsync(inactive.Id, new AddChoirMembersRequest { MemberIds = [member.Id] }, _ct);

        Assert.Equal(ErrorCodes.ChoirInactive, result.Code);
        Assert.Empty(_addedMemberships);
    }

    [Theory]
    [InlineData(MemberStatus.Inactive)]
    [InlineData(MemberStatus.Left)]
    public async Task AddMembers_MemberNotActive_ReturnsInvalid_Async(MemberStatus status)
    {
        var active = NewMember();
        var notActive = NewMember(status);

        var result = await _sut.AddMembersAsync(
            _choir.Id, new AddChoirMembersRequest { MemberIds = [active.Id, notActive.Id] }, _ct);

        Assert.Equal(ErrorCodes.ChoirMemberInvalid, result.Code);
        Assert.Empty(_addedMemberships);
        await _memberships.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task AddMembers_UnknownMember_ReturnsInvalid_Async()
    {
        var result = await _sut.AddMembersAsync(
            _choir.Id, new AddChoirMembersRequest { MemberIds = [Guid.NewGuid()] }, _ct);

        Assert.Equal(ErrorCodes.ChoirMemberInvalid, result.Code);
    }

    [Fact]
    public async Task AddMembers_AddsNew_SkipsCurrent_ReactivatesLeft_Async()
    {
        var fresh = NewMember();
        var current = NewMember();
        var returning = NewMember();
        var currentRow = NewMembership(current);
        var leftRow = NewMembership(returning, isActive: false);

        var result = await _sut.AddMembersAsync(
            _choir.Id, new AddChoirMembersRequest { MemberIds = [fresh.Id, current.Id, returning.Id, fresh.Id] }, _ct);

        Assert.True(result.IsSuccess);
        var added = Assert.Single(_addedMemberships);
        Assert.Equal(fresh.Id, added.MemberId);
        Assert.Equal(_choir.Id, added.ChoirId);
        Assert.True(added.IsActive);
        Assert.True(leftRow.IsActive);
        Assert.Equal(VietnamTime.Today, leftRow.JoinedDate);
        Assert.Equal(new DateOnly(2025, 1, 1), currentRow.JoinedDate);
        await _memberships.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task RemoveMember_MarksMembershipAsLeft_Async()
    {
        var member = NewMember();
        var row = NewMembership(member);

        var result = await _sut.RemoveMemberAsync(_choir.Id, member.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.False(row.IsActive);
        await _memberships.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task RemoveMember_NotACurrentMember_ReturnsNotFound_Async()
    {
        var left = NewMember();
        NewMembership(left, isActive: false);

        var result = await _sut.RemoveMemberAsync(_choir.Id, left.Id, _ct);

        Assert.Equal(ErrorCodes.ChoirMembershipNotFound, result.Code);
        await _memberships.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task GetEventChoirs_ReturnsAssignedChoirsOrderedByName_Async()
    {
        AssignToEvent(_choir);
        AssignToEvent(NewChoir("Agnes Choir"));
        NewChoir("Unassigned Choir");

        var result = await _sut.GetEventChoirsAsync(_event.Id, _ct);

        Assert.Equal(["Agnes Choir", "Cecilia Choir"], result.Value!.Select(x => x.Name));
    }

    [Fact]
    public async Task GetEventChoirs_EventMissing_ReturnsNotFound_Async()
    {
        var result = await _sut.GetEventChoirsAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.LiturgicalEventNotFound, result.Code);
    }

    [Fact]
    public async Task UpdateEventChoirs_AddsNew_RemovesDropped_KeepsInactiveAlreadyAssigned_Async()
    {
        var dropped = NewChoir("Dropped Choir");
        var keptInactive = NewChoir("Kept Choir", isActive: false);
        var added = NewChoir("Added Choir");
        var droppedRow = AssignToEvent(dropped);
        AssignToEvent(keptInactive);

        var result = await _sut.UpdateEventChoirsAsync(
            _event.Id, new UpdateEventChoirsRequest { ChoirIds = [keptInactive.Id, added.Id] }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Added Choir", "Kept Choir"], result.Value!.Select(x => x.Name));
        Assert.Equal(added.Id, Assert.Single(_addedEventChoirs).ChoirId);
        Assert.Same(droppedRow, Assert.Single(_removedEventChoirs));
        await _eventChoirs.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task UpdateEventChoirs_NewInactiveChoir_ReturnsInactive_Async()
    {
        var inactive = NewChoir("Old Choir", isActive: false);

        var result = await _sut.UpdateEventChoirsAsync(
            _event.Id, new UpdateEventChoirsRequest { ChoirIds = [inactive.Id] }, _ct);

        Assert.Equal(ErrorCodes.ChoirInactive, result.Code);
        await _eventChoirs.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task UpdateEventChoirs_UnknownChoir_ReturnsNotFound_Async()
    {
        var result = await _sut.UpdateEventChoirsAsync(
            _event.Id, new UpdateEventChoirsRequest { ChoirIds = [_choir.Id, Guid.NewGuid()] }, _ct);

        Assert.Equal(ErrorCodes.ChoirNotFound, result.Code);
    }

    [Fact]
    public async Task UpdateEventChoirs_CancelledEvent_IsLocked_Async()
    {
        _event.Status = EventStatus.Cancelled;

        var result = await _sut.UpdateEventChoirsAsync(
            _event.Id, new UpdateEventChoirsRequest { ChoirIds = [_choir.Id] }, _ct);

        Assert.Equal(ErrorCodes.LiturgicalEventCancelled, result.Code);
    }

    [Fact]
    public async Task UpdateEventChoirs_PastEvent_IsLocked_Async()
    {
        _event.EventDate = VietnamTime.Today.AddDays(-1);

        var result = await _sut.UpdateEventChoirsAsync(
            _event.Id, new UpdateEventChoirsRequest { ChoirIds = [_choir.Id] }, _ct);

        Assert.Equal(ErrorCodes.LiturgicalEventAlreadyPassed, result.Code);
    }

    [Fact]
    public async Task UpdateEventChoirs_EventMissing_ReturnsNotFound_Async()
    {
        var result = await _sut.UpdateEventChoirsAsync(
            Guid.NewGuid(), new UpdateEventChoirsRequest { ChoirIds = [_choir.Id] }, _ct);

        Assert.Equal(ErrorCodes.LiturgicalEventNotFound, result.Code);
    }
}
