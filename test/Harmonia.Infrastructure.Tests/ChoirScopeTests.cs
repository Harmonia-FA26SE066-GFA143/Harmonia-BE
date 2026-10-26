using Harmonia.Application.DTOs;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Repositories;
using NSubstitute;

namespace Harmonia.Infrastructure.Tests;

/// <summary>
/// D8 director scope on a real database: <see cref="ChoirScopeService"/> and the repository filters that repeat its
/// rules in SQL. "Mine" is led by the calling director, "Theirs" by another one.
/// </summary>
public sealed class ChoirScopeTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Guid _meId;
    private readonly Choir _mine;
    private readonly Choir _theirs;
    private readonly Choir _mineInactive;
    private readonly Guid _inMine;
    private readonly Guid _inTheirs;
    private readonly WorshipLocation _location = new() { Id = Guid.NewGuid(), Name = "Main church" };

    public ChoirScopeTests()
    {
        var me = _db.AddUserAsync("me@test.com", RoleNames.ChoirDirector, _ct).GetAwaiter().GetResult();
        var other = _db.AddUserAsync("other@test.com", RoleNames.ChoirDirector, _ct).GetAwaiter().GetResult();
        _meId = me.Id;
        _mine = new Choir { Id = Guid.NewGuid(), Name = "Mine", DirectorId = me.Id };
        _theirs = new Choir { Id = Guid.NewGuid(), Name = "Theirs", DirectorId = other.Id };
        _mineInactive = new Choir { Id = Guid.NewGuid(), Name = "Mine old", DirectorId = me.Id, IsActive = false };
        using (var seed = _db.NewContext())
        {
            seed.Choirs.AddRange(_mine, _theirs, _mineInactive);
            seed.WorshipLocations.Add(_location);
            seed.SaveChanges();
        }

        _inMine = AddMemberAsync("In Mine", _mine.Id).GetAwaiter().GetResult();
        _inTheirs = AddMemberAsync("In Theirs", _theirs.Id).GetAwaiter().GetResult();

        _db.CurrentUser.RoleName.Returns(RoleNames.ChoirDirector);
        _db.CurrentUser.UserId.Returns(_meId);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Guid> AddMemberAsync(string fullName, Guid? choirId)
    {
        var user = await _db.AddUserAsync($"{Guid.NewGuid()}@test.com", RoleNames.ChoirMember, _ct, fullName);
        var member = new MemberProfile { Id = Guid.NewGuid(), UserId = user.Id, Status = MemberStatus.Active };
        await using var context = _db.NewContext();
        context.MemberProfiles.Add(member);
        if (choirId is { } id)
        {
            context.ChoirMemberships.Add(new ChoirMembership
            {
                Id = Guid.NewGuid(), ChoirId = id, MemberId = member.Id, JoinedDate = new DateOnly(2026, 1, 1),
            });
        }

        await context.SaveChangesAsync(_ct);
        return member.Id;
    }

    private int _eventCount;

    /// <summary>A published event, each at its own hour; each choir tuple is (choir id, is lead).</summary>
    private async Task<Guid> AddEventAsync(params (Guid ChoirId, bool IsLead)[] choirs)
    {
        var liturgicalEvent = new LiturgicalEvent
        {
            Id = Guid.NewGuid(), EventDate = VietnamTime.Today.AddDays(7), Time = new TimeOnly(6 + _eventCount++, 0),
            LocationId = _location.Id, Status = EventStatus.Published,
        };
        await using var context = _db.NewContext();
        context.LiturgicalEvents.Add(liturgicalEvent);
        context.LiturgicalEventChoirs.AddRange(choirs.Select(c => new LiturgicalEventChoir
        {
            Id = Guid.NewGuid(), EventId = liturgicalEvent.Id, ChoirId = c.ChoirId, IsLead = c.IsLead,
        }));
        await context.SaveChangesAsync(_ct);
        return liturgicalEvent.Id;
    }

    private ChoirScopeService NewScope(Data.HarmoniaDbContext context) => new(
        new GenericRepository<Choir>(context), new GenericRepository<ChoirMembership>(context),
        new GenericRepository<LiturgicalEventChoir>(context), _db.CurrentUser);

    [Fact]
    public async Task CanManageChoir_OwnOrNoChoir_ButNotAnotherDirectors_Async()
    {
        await using var context = _db.NewContext();
        var scope = NewScope(context);

        Assert.Equal(new[] { _mine.Id, _mineInactive.Id }.Order(), (await scope.GetDirectedChoirIdsAsync(_ct))!.Order());
        Assert.True(await scope.CanManageChoirAsync(_mine.Id, _ct));
        Assert.True(await scope.CanManageChoirAsync(null, _ct));
        Assert.False(await scope.CanManageChoirAsync(_theirs.Id, _ct));
    }

    [Fact]
    public async Task OtherRoles_AreNotLimited_Async()
    {
        _db.CurrentUser.RoleName.Returns(RoleNames.ParishPriest);
        var eventId = await AddEventAsync((_theirs.Id, true));
        await using var context = _db.NewContext();
        var scope = NewScope(context);

        Assert.Null(await scope.GetDirectedChoirIdsAsync(_ct));
        Assert.True(await scope.CanManageChoirAsync(_theirs.Id, _ct));
        Assert.True(await scope.CanLeadEventAsync(eventId, _ct));
    }

    [Fact]
    public async Task CheckChoirForNewRecord_CoversOwnershipActivityAndEventChoirs_Async()
    {
        var servedByTheirs = await AddEventAsync((_theirs.Id, true));
        var servedByMine = await AddEventAsync((_mine.Id, true));
        var noChoirEvent = await AddEventAsync();
        await using var context = _db.NewContext();
        var scope = NewScope(context);

        Assert.Null(await scope.CheckChoirForNewRecordAsync(_mine.Id, null, _ct));
        Assert.Null(await scope.CheckChoirForNewRecordAsync(_mine.Id, servedByMine, _ct));
        Assert.Null(await scope.CheckChoirForNewRecordAsync(_mine.Id, noChoirEvent, _ct));
        Assert.Equal(ErrorCodes.ChoirNotServingEvent, await scope.CheckChoirForNewRecordAsync(_mine.Id, servedByTheirs, _ct));
        Assert.Equal(ErrorCodes.ChoirNotFound, await scope.CheckChoirForNewRecordAsync(_theirs.Id, null, _ct));
        Assert.Equal(ErrorCodes.ChoirNotFound, await scope.CheckChoirForNewRecordAsync(Guid.NewGuid(), null, _ct));
        Assert.Equal(ErrorCodes.ChoirInactive, await scope.CheckChoirForNewRecordAsync(_mineInactive.Id, null, _ct));
    }

    [Fact]
    public async Task EventAccess_LeadPlans_ServingChoirViews_OthersSeeNothing_Async()
    {
        var iLead = await AddEventAsync((_mine.Id, true), (_theirs.Id, false));
        var iServe = await AddEventAsync((_mine.Id, false), (_theirs.Id, true));
        var notMine = await AddEventAsync((_theirs.Id, true));
        var noChoir = await AddEventAsync();
        var noLeadMarked = await AddEventAsync((_mine.Id, false), (_theirs.Id, false));
        await using var context = _db.NewContext();
        var scope = NewScope(context);

        Assert.Null(await scope.CheckCanLeadEventAsync(iLead, "HIDDEN", _ct));
        Assert.Equal(ErrorCodes.ChoirNotLead, await scope.CheckCanLeadEventAsync(iServe, "HIDDEN", _ct));
        Assert.Equal("HIDDEN", await scope.CheckCanLeadEventAsync(notMine, "HIDDEN", _ct));
        Assert.Null(await scope.CheckCanLeadEventAsync(noChoir, "HIDDEN", _ct));
        Assert.Null(await scope.CheckCanLeadEventAsync(noLeadMarked, "HIDDEN", _ct));
        Assert.True(await scope.CanViewEventAsync(iServe, _ct));
        Assert.False(await scope.CanViewEventAsync(notMine, _ct));
        Assert.Equal(_mine.Id, await scope.GetLeadChoirIdAsync(iLead, _ct));
        Assert.Null(await scope.GetLeadChoirIdAsync(noLeadMarked, _ct));
    }

    [Fact]
    public async Task GetEventMemberIds_MembersOfServingChoirs_NullWithoutChoirs_Async()
    {
        var both = await AddEventAsync((_mine.Id, true), (_theirs.Id, false));
        var mineOnly = await AddEventAsync((_mine.Id, true));
        var noChoir = await AddEventAsync();
        await using var context = _db.NewContext();
        var scope = NewScope(context);

        Assert.Equal(new[] { _inMine, _inTheirs }.Order(), (await scope.GetEventMemberIdsAsync(both, _ct))!.Order());
        Assert.Equal([_inMine], await scope.GetEventMemberIdsAsync(mineOnly, _ct));
        Assert.Null(await scope.GetEventMemberIdsAsync(noChoir, _ct));
    }

    [Fact]
    public async Task RehearsalSearch_ChoirIds_KeepsOwnAndUnscopedRehearsals_Async()
    {
        var start = DateTime.UtcNow.AddDays(1);
        Rehearsal New(Guid? choirId) => new()
        {
            Id = Guid.NewGuid(), ChoirId = choirId, StartTime = start, EndTime = start.AddHours(1),
        };
        var mine = New(_mine.Id);
        var theirs = New(_theirs.Id);
        var legacy = New(null);
        await using (var seed = _db.NewContext())
        {
            seed.Rehearsals.AddRange(mine, theirs, legacy);
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var repository = new RehearsalRepository(context);
        var page = await repository.SearchAsync(new SearchRehearsalsRequest(), [_mine.Id], _ct);
        var upcoming = await repository.GetUpcomingAsync(DateTime.UtcNow, [_mine.Id], _ct);

        Assert.Equal(new[] { mine.Id, legacy.Id }.Order(), page.Items.Select(x => x.Id).Order());
        Assert.Equal(new[] { mine.Id, legacy.Id }.Order(), upcoming.Select(x => x.Id).Order());
    }

    [Fact]
    public async Task RehearsalStartingBetween_PastRange_KeepsOwnChoirAndParishDayBounds_Async()
    {
        var day = new DateOnly(2020, 3, 10);
        Rehearsal New(Guid? choirId, DateTime start) => new()
        {
            Id = Guid.NewGuid(), ChoirId = choirId, StartTime = start, EndTime = start.AddHours(1),
        };
        var firstMinute = New(_mine.Id, VietnamTime.StartOfDayUtc(day));
        var lastMinute = New(null, VietnamTime.StartOfDayUtc(day.AddDays(1)).AddMinutes(-1));
        var theirs = New(_theirs.Id, VietnamTime.StartOfDayUtc(day).AddHours(2));
        var nextDay = New(_mine.Id, VietnamTime.StartOfDayUtc(day.AddDays(1)));
        await using (var seed = _db.NewContext())
        {
            seed.Rehearsals.AddRange(firstMinute, lastMinute, theirs, nextDay);
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var found = await new RehearsalRepository(context).GetStartingBetweenAsync(day, day, [_mine.Id], _ct);

        Assert.Equal([firstMinute.Id, lastMinute.Id], found.Select(x => x.Id));
    }

    [Fact]
    public async Task PracticeAssignmentsForMember_ChoirAssignmentReachesOnlyItsMembers_Async()
    {
        PracticeAssignment New(Guid? choirId) => new()
        {
            Id = Guid.NewGuid(), ChoirId = choirId, Title = "Learn", Scope = AssignmentScope.All,
            DueDate = DateTime.UtcNow.AddDays(3),
        };
        var mine = New(_mine.Id);
        var theirs = New(_theirs.Id);
        var legacy = New(null);
        await using (var seed = _db.NewContext())
        {
            seed.PracticeAssignments.AddRange(mine, theirs, legacy);
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var page = await new PracticeAssignmentRepository(context)
            .GetForMemberAsync(_inMine, new SearchMyPracticeAssignmentsRequest(), _ct);

        Assert.Equal(new[] { mine.Id, legacy.Id }.Order(), page.Items.Select(x => x.Id).Order());
    }

    [Fact]
    public async Task EventSearch_ChoirIds_KeepsEventsOfOwnChoirsOrWithoutChoirs_Async()
    {
        var served = await AddEventAsync((_theirs.Id, true), (_mine.Id, false));
        var notMine = await AddEventAsync((_theirs.Id, true));
        var noChoir = await AddEventAsync();
        await using var context = _db.NewContext();

        var page = await new LiturgicalEventRepository(context)
            .SearchAsync(new SearchLiturgicalEventsRequest(), false, [_mine.Id], _ct);

        Assert.Equal(new[] { served, noChoir }.Order(), page.Items.Select(x => x.Id).Order());
        Assert.DoesNotContain(notMine, page.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task ActiveForEventAndReports_KeepOnlyScopedMembers_Async()
    {
        var noChoirMember = await AddMemberAsync("No Choir", null);
        var mineOnly = await AddEventAsync((_mine.Id, true));
        await using var context = _db.NewContext();
        var repository = new MemberProfileRepository(context);

        var forEvent = await repository.GetActiveForEventAsync(mineOnly, _ct);
        var report = await repository.GetActiveWithActivityAsync(new SearchReportsRequest(), _meId, _ct);

        Assert.Equal([_inMine], forEvent.Select(x => x.Id));
        Assert.Equal(new[] { _inMine, noChoirMember }.Order(), report.Items.Select(x => x.Id).Order());
    }
}
