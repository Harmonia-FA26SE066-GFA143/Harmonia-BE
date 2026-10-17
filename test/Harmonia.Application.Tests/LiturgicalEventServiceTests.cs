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

public class LiturgicalEventServiceTests
{
    private readonly ILiturgicalEventRepository _repository = Substitute.For<ILiturgicalEventRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly ILookupService _lookups = Substitute.For<ILookupService>();
    private readonly LiturgicalEventService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly LiturgicalEvent _event = new()
    {
        Id = Guid.NewGuid(),
        EventDate = VietnamTime.Today.AddDays(3),
        Time = new TimeOnly(8, 0),
        LocationId = Guid.NewGuid(),
        Location = new WorshipLocation { Name = "Main church" },
        MassTypeId = Guid.NewGuid(),
        Status = EventStatus.Published,
    };

    public LiturgicalEventServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<LiturgicalEventProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new LiturgicalEventService(_repository, _users, _notifications, _lookups, mapper);
        _lookups.CheckEventCatalogsAsync(default, default, default, default, default, default, default, default)
            .ReturnsForAnyArgs(Result.Success());
        _repository.GetWithLocationAsync(_event.Id, _ct).Returns(_event);
    }

    private UpdateLiturgicalEventRequest UpdateRequest(Guid? locationId = null) => new()
    {
        EventDate = _event.EventDate.AddDays(1),
        Time = new TimeOnly(9, 30),
        LocationId = locationId ?? _event.LocationId,
        MassTypeId = _event.MassTypeId,
        Title = "Updated",
    };

    [Fact]
    public async Task Update_PublishedEvent_ChangesFieldsAndChecksSlotExcludingItself_Async()
    {
        var request = UpdateRequest();

        var result = await _sut.UpdateAsync(_event.Id, request, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(request.EventDate, _event.EventDate);
        Assert.Equal("Updated", result.Value!.Title);
        Assert.Equal(EventStatus.Published, _event.Status);
        await _repository.Received(1).ExistsBySlotAsync(request.EventDate, request.Time, request.LocationId, _event.Id, _ct);
        await _repository.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Create_CatalogCheckFails_ReturnsItsCode_AndAddsNothing_Async()
    {
        var request = new CreateLiturgicalEventRequest
        {
            EventDate = _event.EventDate, Time = _event.Time, LocationId = Guid.NewGuid(), MassTypeId = Guid.NewGuid(),
            LiturgicalSeasonId = Guid.NewGuid(), CeremonyTypeId = Guid.NewGuid(), CategoryId = Guid.NewGuid(),
        };
        _lookups.CheckEventCatalogsAsync(
                request.EventDate, request.LiturgicalSeasonId, request.MassTypeId, request.CeremonyTypeId,
                request.CategoryId, request.LocationId, null, _ct)
            .Returns(Result.Failure(ErrorCodes.LookupInactive));

        var result = await _sut.CreateAsync(request, _ct);

        Assert.Equal(ErrorCodes.LookupInactive, result.Code);
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Update_CatalogCheckFails_ReturnsItsCode_AndIsGivenTheCurrentEvent_Async()
    {
        var request = UpdateRequest();
        _lookups.CheckEventCatalogsAsync(
                request.EventDate, request.LiturgicalSeasonId, request.MassTypeId, request.CeremonyTypeId,
                request.CategoryId, request.LocationId, _event, _ct)
            .Returns(Result.Failure(ErrorCodes.LiturgicalEventDateOutsideLiturgicalSeason));

        var result = await _sut.UpdateAsync(_event.Id, request, _ct);

        Assert.Equal(ErrorCodes.LiturgicalEventDateOutsideLiturgicalSeason, result.Code);
        await _repository.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Update_SlotTakenByAnotherEvent_ReturnsSlotTaken_Async()
    {
        _repository.ExistsBySlotAsync(Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<Guid>(), _event.Id, _ct).Returns(true);

        var result = await _sut.UpdateAsync(_event.Id, UpdateRequest(), _ct);

        Assert.Equal(ErrorCodes.LiturgicalEventSlotTaken, result.Code);
        await _repository.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Update_CancelledEvent_ReturnsEventCancelled_Async()
    {
        _event.Status = EventStatus.Cancelled;

        Assert.Equal(ErrorCodes.LiturgicalEventCancelled, (await _sut.UpdateAsync(_event.Id, UpdateRequest(), _ct)).Code);
    }

    [Fact]
    public async Task Update_MissingEvent_ReturnsEventNotFound_Async()
    {
        Assert.Equal(ErrorCodes.LiturgicalEventNotFound, (await _sut.UpdateAsync(Guid.NewGuid(), UpdateRequest(), _ct)).Code);
    }

    [Fact]
    public async Task Cancel_PublishedEvent_CancelsAndNotifiesDirectorsAndMembers_Async()
    {
        var recipients = new List<Guid> { Guid.NewGuid() };
        _users.GetActiveUserIdsByRolesAsync(Arg.Any<IReadOnlyCollection<string>>(), _ct).Returns(recipients);

        var result = await _sut.CancelAsync(_event.Id, _ct);

        Assert.Equal(EventStatus.Cancelled, result.Value!.Status);
        await _repository.Received(1).SaveChangesAsync(_ct);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(x => x.Type == NotificationType.EventCancelled
                && x.ReferenceId == _event.Id && x.RecipientUserIds == recipients),
            _ct);
    }

    [Fact]
    public async Task Cancel_DraftEvent_CancelsWithoutNotification_Async()
    {
        _event.Status = EventStatus.Draft;

        var result = await _sut.CancelAsync(_event.Id, _ct);

        Assert.Equal(EventStatus.Cancelled, result.Value!.Status);
        await _notifications.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_ReturnsEventCancelled_Async()
    {
        _event.Status = EventStatus.Cancelled;

        Assert.Equal(ErrorCodes.LiturgicalEventCancelled, (await _sut.CancelAsync(_event.Id, _ct)).Code);
    }

    [Fact]
    public async Task Cancel_PastEvent_ReturnsEventAlreadyPassed_Async()
    {
        _event.EventDate = VietnamTime.Today.AddDays(-1);

        Assert.Equal(ErrorCodes.LiturgicalEventAlreadyPassed, (await _sut.CancelAsync(_event.Id, _ct)).Code);
        await _repository.DidNotReceive().SaveChangesAsync(_ct);
    }
}
