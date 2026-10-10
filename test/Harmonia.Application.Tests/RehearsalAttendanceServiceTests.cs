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

public class RehearsalAttendanceServiceTests
{
    private readonly IRehearsalRepository _rehearsals = Substitute.For<IRehearsalRepository>();
    private readonly IMemberProfileRepository _members = Substitute.For<IMemberProfileRepository>();
    private readonly RehearsalAttendanceService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Guid _directorUserId = Guid.NewGuid();
    private readonly Rehearsal _rehearsal;
    private readonly MemberProfile _alice;
    private readonly MemberProfile _bob;

    public RehearsalAttendanceServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MemberProfileProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new RehearsalAttendanceService(_rehearsals, _members, mapper);

        _rehearsal = new Rehearsal
        {
            Id = Guid.NewGuid(), StartTime = DateTime.UtcNow.AddHours(-1), EndTime = DateTime.UtcNow.AddHours(1)
        };
        _alice = new MemberProfile { Id = Guid.NewGuid(), Status = MemberStatus.Active, User = new User { FullName = "Alice" } };
        _bob = new MemberProfile { Id = Guid.NewGuid(), Status = MemberStatus.Active, User = new User { FullName = "Bob" } };

        _rehearsals.GetByIdAsync(_rehearsal.Id, _ct).Returns(_rehearsal);
        _rehearsals.GetWithAttendancesForUpdateAsync(_rehearsal.Id, _ct).Returns(_rehearsal);
        _rehearsals.TrySaveAttendancesAsync(_rehearsal, _ct).Returns(true);
        _members.ListAsync(Arg.Any<Expression<Func<MemberProfile, bool>>>(), _ct)
            .Returns(call => new[] { _alice, _bob }.AsQueryable().Where(call.Arg<Expression<Func<MemberProfile, bool>>>()).ToList());
    }

    private static RecordRehearsalAttendancesRequest Request(params (Guid MemberId, AttendanceStatus Status)[] items) =>
        new() { Items = items.Select(i => new RecordRehearsalAttendanceRequest { MemberId = i.MemberId, Status = i.Status }).ToList() };

    [Fact]
    public async Task Record_NewAndExisting_AddsAndCorrectsRows_Async()
    {
        var existing = new RehearsalAttendance
        {
            Id = Guid.NewGuid(), RehearsalId = _rehearsal.Id, MemberId = _bob.Id, Status = AttendanceStatus.Absent,
            CheckedBy = Guid.NewGuid(), CheckedAt = DateTime.UtcNow.AddMinutes(-30)
        };
        _rehearsal.Attendances.Add(existing);

        var result = await _sut.RecordAsync(_directorUserId, _rehearsal.Id,
            Request((_alice.Id, AttendanceStatus.Present), (_bob.Id, AttendanceStatus.Late)), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _rehearsal.Attendances.Count);
        var alice = Assert.Single(_rehearsal.Attendances, a => a.MemberId == _alice.Id);
        Assert.Equal(AttendanceStatus.Present, alice.Status);
        Assert.Equal(Guid.Empty, alice.Id);
        Assert.Equal(AttendanceStatus.Late, existing.Status);
        Assert.All(_rehearsal.Attendances, a => Assert.Equal(_directorUserId, a.CheckedBy));
        await _rehearsals.Received(1).TrySaveAttendancesAsync(_rehearsal, _ct);
    }

    [Fact]
    public async Task Record_RehearsalMissing_ReturnsNotFound_Async()
    {
        var result = await _sut.RecordAsync(_directorUserId, Guid.NewGuid(), Request((_alice.Id, AttendanceStatus.Present)), _ct);

        Assert.Equal(ErrorCodes.RehearsalNotFound, result.Code);
    }

    [Fact]
    public async Task Record_BeforeStart_ReturnsNotStarted_Async()
    {
        _rehearsal.StartTime = DateTime.UtcNow.AddHours(1);

        var result = await _sut.RecordAsync(_directorUserId, _rehearsal.Id, Request((_alice.Id, AttendanceStatus.Present)), _ct);

        Assert.Equal(ErrorCodes.RehearsalNotStarted, result.Code);
        await _rehearsals.DidNotReceive().TrySaveAttendancesAsync(Arg.Any<Rehearsal>(), _ct);
    }

    [Fact]
    public async Task Record_AfterEnd_IsAllowed_Async()
    {
        _rehearsal.StartTime = DateTime.UtcNow.AddDays(-2);
        _rehearsal.EndTime = DateTime.UtcNow.AddDays(-2).AddHours(2);

        var result = await _sut.RecordAsync(_directorUserId, _rehearsal.Id, Request((_alice.Id, AttendanceStatus.Excused)), _ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Record_UnknownMember_ReturnsMemberNotFound_Async()
    {
        var result = await _sut.RecordAsync(_directorUserId, _rehearsal.Id, Request((Guid.NewGuid(), AttendanceStatus.Present)), _ct);

        Assert.Equal(ErrorCodes.MemberNotFound, result.Code);
        Assert.Empty(_rehearsal.Attendances);
    }

    [Fact]
    public async Task Record_InactiveMember_ReturnsMemberNotActive_Async()
    {
        _bob.Status = MemberStatus.Left;

        var result = await _sut.RecordAsync(_directorUserId, _rehearsal.Id,
            Request((_alice.Id, AttendanceStatus.Present), (_bob.Id, AttendanceStatus.Present)), _ct);

        Assert.Equal(ErrorCodes.MemberNotActive, result.Code);
        Assert.Empty(_rehearsal.Attendances);
    }

    [Fact]
    public async Task Record_ConcurrentInsert_ReturnsAlreadyRecorded_Async()
    {
        _rehearsals.TrySaveAttendancesAsync(_rehearsal, _ct).Returns(false);

        var result = await _sut.RecordAsync(_directorUserId, _rehearsal.Id, Request((_alice.Id, AttendanceStatus.Present)), _ct);

        Assert.Equal(ErrorCodes.AttendanceAlreadyRecorded, result.Code);
    }

    [Fact]
    public async Task GetAttendances_MapsRecordedAndUnrecordedMembers_Async()
    {
        var checkedAt = DateTime.UtcNow;
        _bob.RehearsalAttendances.Add(new RehearsalAttendance { MemberId = _bob.Id, Status = AttendanceStatus.Late, CheckedAt = checkedAt });
        _members.GetAttendanceRosterAsync(_rehearsal.Id, _ct).Returns([_alice, _bob]);

        var result = await _sut.GetAttendancesAsync(_rehearsal.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Collection(result.Value!,
            a => { Assert.Equal("Alice", a.FullName); Assert.Null(a.Status); Assert.Null(a.CheckedAt); },
            b => { Assert.Equal(_bob.Id, b.MemberId); Assert.Equal(AttendanceStatus.Late, b.Status); Assert.Equal(checkedAt, b.CheckedAt); });
    }

    [Fact]
    public async Task GetAttendances_RehearsalMissing_ReturnsNotFound_Async()
    {
        var result = await _sut.GetAttendancesAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.RehearsalNotFound, result.Code);
    }
}
