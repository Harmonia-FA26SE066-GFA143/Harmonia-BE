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

public class MassScheduleServiceTests
{
    private readonly IGenericRepository<MassSchedule> _schedules = Substitute.For<IGenericRepository<MassSchedule>>();
    private readonly IGenericRepository<MassType> _massTypes = Substitute.For<IGenericRepository<MassType>>();
    private readonly IGenericRepository<WorshipLocation> _locations = Substitute.For<IGenericRepository<WorshipLocation>>();
    private readonly IGenericRepository<LiturgicalSeason> _seasons = Substitute.For<IGenericRepository<LiturgicalSeason>>();
    private readonly ILiturgicalEventRepository _events = Substitute.For<ILiturgicalEventRepository>();
    private readonly MassScheduleService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly MassType _massType = new() { Id = Guid.NewGuid(), Name = "Sunday Mass" };
    private readonly WorshipLocation _location = new() { Id = Guid.NewGuid(), Name = "Main church" };
    private readonly List<MassSchedule> _scheduleRows = [];
    private readonly List<LiturgicalSeason> _seasonRows = [];
    private readonly List<LiturgicalEvent> _eventRows = [];
    private readonly List<LiturgicalEvent> _addedEvents = [];

    public MassScheduleServiceTests()
    {
        var mapper = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MassScheduleProfile>();
                cfg.AddProfile<LiturgicalEventProfile>();
            }, NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new MassScheduleService(_schedules, _massTypes, _locations, _seasons, _events, mapper);

        // The fakes evaluate the service's predicate against in-memory rows, as the database would.
        _schedules.ListAsync(Arg.Any<Expression<Func<MassSchedule, bool>>>(), _ct)
            .Returns(call => _scheduleRows.Where(call.Arg<Expression<Func<MassSchedule, bool>>>().Compile()).ToList());
        _schedules.GetByIdAsync(Arg.Any<Guid>(), _ct)
            .Returns(call => _scheduleRows.SingleOrDefault(x => x.Id == call.Arg<Guid>()));
        _seasons.ListAsync(Arg.Any<Expression<Func<LiturgicalSeason, bool>>>(), _ct)
            .Returns(call => _seasonRows.Where(call.Arg<Expression<Func<LiturgicalSeason, bool>>>().Compile()).ToList());
        _events.ListAsync(Arg.Any<Expression<Func<LiturgicalEvent, bool>>>(), _ct)
            .Returns(call => _eventRows.Where(call.Arg<Expression<Func<LiturgicalEvent, bool>>>().Compile()).ToList());
        _events.AddAsync(Arg.Do<LiturgicalEvent>(_addedEvents.Add), _ct).Returns(Task.CompletedTask);
        _massTypes.GetByIdAsync(_massType.Id, _ct).Returns(_massType);
        _locations.GetByIdAsync(_location.Id, _ct).Returns(_location);
    }

    private MassSchedule AddSchedule(DayOfWeek day, int hour)
    {
        var schedule = new MassSchedule
        {
            Id = Guid.NewGuid(), DayOfWeek = day, Time = new TimeOnly(hour, 0),
            MassTypeId = _massType.Id, MassType = _massType, LocationId = _location.Id, Location = _location,
        };
        _scheduleRows.Add(schedule);
        return schedule;
    }

    private CreateMassScheduleRequest CreateRequest(int hour = 4) => new()
    {
        DayOfWeek = DayOfWeek.Sunday, Time = new TimeOnly(hour, 0), MassTypeId = _massType.Id, LocationId = _location.Id,
        Title = "  Early Mass ",
    };

    private static DateOnly NextSunday()
    {
        var today = VietnamTime.Today;
        return today.AddDays(((int)DayOfWeek.Sunday - (int)today.DayOfWeek + 7) % 7);
    }

    [Fact]
    public async Task Create_FreeSlot_AddsTrimmedSchedule_Async()
    {
        var result = await _sut.CreateAsync(CreateRequest(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Early Mass", result.Value!.Title);
        await _schedules.Received(1).AddAsync(Arg.Any<MassSchedule>(), _ct);
        await _schedules.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Create_SameDayTimeLocation_ReturnsSlotTaken_Async()
    {
        AddSchedule(DayOfWeek.Sunday, 4);

        Assert.Equal(ErrorCodes.MassScheduleSlotTaken, (await _sut.CreateAsync(CreateRequest(), _ct)).Code);
        await _schedules.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Create_InactiveMassType_ReturnsLookupInactive_Async()
    {
        _massType.IsActive = false;

        Assert.Equal(ErrorCodes.LookupInactive, (await _sut.CreateAsync(CreateRequest(), _ct)).Code);
    }

    [Fact]
    public async Task Create_UnknownLocation_ReturnsLookupNotFound_Async()
    {
        var request = CreateRequest();
        request.LocationId = Guid.NewGuid();

        Assert.Equal(ErrorCodes.LookupNotFound, (await _sut.CreateAsync(request, _ct)).Code);
    }

    [Fact]
    public async Task Update_KeepsSwitchedOffLocation_AndChecksSlotExcludingItself_Async()
    {
        var schedule = AddSchedule(DayOfWeek.Sunday, 4);
        _location.IsActive = false;

        var result = await _sut.UpdateAsync(schedule.Id, new UpdateMassScheduleRequest
        {
            DayOfWeek = DayOfWeek.Sunday, Time = schedule.Time, MassTypeId = _massType.Id, LocationId = _location.Id,
            IsActive = false,
        }, _ct);

        Assert.True(result.IsSuccess);
        Assert.False(schedule.IsActive);
    }

    [Fact]
    public async Task Update_Missing_ReturnsNotFound_Async()
    {
        var result = await _sut.UpdateAsync(Guid.NewGuid(), new UpdateMassScheduleRequest(), _ct);

        Assert.Equal(ErrorCodes.MassScheduleNotFound, result.Code);
    }

    [Fact]
    public async Task Generate_CreatesDraftsOnMatchingDays_SkipsTakenAndInactive_FillsSeason_Async()
    {
        var sunday = NextSunday();
        AddSchedule(DayOfWeek.Sunday, 4);
        AddSchedule(DayOfWeek.Sunday, 7);
        AddSchedule(DayOfWeek.Sunday, 15).IsActive = false;
        AddSchedule(DayOfWeek.Monday, 5);
        var season = new LiturgicalSeason { Id = Guid.NewGuid(), StartDate = sunday, EndDate = sunday };
        _seasonRows.Add(season);
        // 07:00 on the first Sunday is already taken; a cancelled event frees its slot.
        _eventRows.Add(new LiturgicalEvent { EventDate = sunday, Time = new TimeOnly(7, 0), LocationId = _location.Id });
        _eventRows.Add(new LiturgicalEvent
        {
            EventDate = sunday, Time = new TimeOnly(4, 0), LocationId = _location.Id, Status = EventStatus.Cancelled,
        });

        var result = await _sut.GenerateEventsAsync(
            new GenerateLiturgicalEventsRequest { FromDate = sunday, ToDate = sunday.AddDays(7) }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [(sunday, new TimeOnly(4, 0)), (sunday.AddDays(1), new TimeOnly(5, 0)),
             (sunday.AddDays(7), new TimeOnly(4, 0)), (sunday.AddDays(7), new TimeOnly(7, 0))],
            _addedEvents.Select(x => (x.EventDate, x.Time)));
        Assert.All(_addedEvents, x => Assert.Equal(EventStatus.Draft, x.Status));
        Assert.Equal(season.Id, _addedEvents[0].LiturgicalSeasonId);
        Assert.Null(_addedEvents[^1].LiturgicalSeasonId);
        await _events.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Generate_NothingToCreate_DoesNotSave_Async()
    {
        var result = await _sut.GenerateEventsAsync(
            new GenerateLiturgicalEventsRequest { FromDate = NextSunday(), ToDate = NextSunday() }, _ct);

        Assert.Empty(result.Value!);
        await _events.DidNotReceive().SaveChangesAsync(_ct);
    }
}
