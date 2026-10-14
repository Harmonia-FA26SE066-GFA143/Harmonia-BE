using AutoMapper;
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

public class EventParticipationServiceTests
{
    private readonly IEventParticipationRepository _participations = Substitute.For<IEventParticipationRepository>();
    private readonly ILiturgicalEventRepository _events = Substitute.For<ILiturgicalEventRepository>();
    private readonly IMemberProfileRepository _members = Substitute.For<IMemberProfileRepository>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly EventParticipationService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly List<EventParticipation> _added = [];
    private readonly List<Guid> _alreadyRequested = [];

    // A month away on either side, so no test depends on the time zone the service compares in.
    private readonly LiturgicalEvent _event = new()
    {
        Id = Guid.NewGuid(), Status = EventStatus.Published,
        EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), Time = new TimeOnly(18, 0),
    };

    private readonly MemberProfile _anna = NewMember("Anna");
    private readonly MemberProfile _ben = NewMember("Ben");

    public EventParticipationServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<EventParticipationProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new EventParticipationService(_participations, _events, _members, _notifications, mapper);

        _events.GetByIdAsync(_event.Id, _ct).Returns(_event);
        _members.GetAllActiveWithUserAsync(_ct).Returns([_anna, _ben]);
        _members.GetByIdsWithUserAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _ct)
            .Returns(call => new[] { _anna, _ben }.Where(m => call.Arg<IReadOnlyCollection<Guid>>().Contains(m.Id)).ToList());
        _participations.GetRequestedMemberIdsAsync(_event.Id, Arg.Any<IReadOnlyCollection<Guid>>(), _ct)
            .Returns(call => _alreadyRequested.Intersect(call.Arg<IReadOnlyCollection<Guid>>()).ToList());
        _participations.AddAsync(Arg.Do<EventParticipation>(_added.Add), _ct).Returns(Task.CompletedTask);
    }

    private static MemberProfile NewMember(string name, MemberStatus status = MemberStatus.Active)
    {
        var user = new User { Id = Guid.NewGuid(), FullName = name };
        return new MemberProfile { Id = Guid.NewGuid(), UserId = user.Id, User = user, Status = status };
    }

    private SendParticipationRequestsRequest Send(params Guid[] memberIds) =>
        new() { EventId = _event.Id, MemberIds = memberIds.Length == 0 ? null : [.. memberIds] };

    private EventParticipation Invitation(MemberProfile member, ParticipationStatus status = ParticipationStatus.Invited)
    {
        var participation = new EventParticipation
        {
            Id = Guid.NewGuid(), EventId = _event.Id, MemberId = member.Id, Status = status,
            LiturgicalEvent = _event, Member = member,
        };
        _members.GetByUserIdAsync(member.UserId, _ct).Returns(member);
        _participations.GetByEventAndMemberAsync(_event.Id, member.Id, _ct).Returns(participation);
        return participation;
    }

    // ---- SendRequests ----

    [Fact]
    public async Task SendRequests_NoMemberIds_InvitesEveryActiveMember_AndNotifiesThem_Async()
    {
        var result = await _sut.SendRequestsAsync(Send(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Anna", "Ben"], result.Value!.Select(x => x.MemberFullName));
        Assert.Equal([_anna.Id, _ben.Id], _added.Select(x => x.MemberId));
        Assert.All(_added, x =>
        {
            Assert.Equal(_event.Id, x.EventId);
            Assert.Equal(ParticipationStatus.Invited, x.Status);
        });
        await _participations.Received(1).SaveChangesAsync(_ct);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(r => r.Type == NotificationType.ParticipationRequest
                && r.RecipientUserIds.SequenceEqual(new[] { _anna.UserId, _ben.UserId })
                && r.ReferenceType == nameof(LiturgicalEvent) && r.ReferenceId == _event.Id),
            _ct);
    }

    [Fact]
    public async Task SendRequests_NoMemberIds_SkipsMembersAlreadyInvited_Async()
    {
        _alreadyRequested.Add(_anna.Id);

        var result = await _sut.SendRequestsAsync(Send(), _ct);

        Assert.Equal([_ben.Id], result.Value!.Select(x => x.MemberId));
    }

    [Fact]
    public async Task SendRequests_NoMemberIds_EveryoneAlreadyInvited_SavesAndNotifiesNothing_Async()
    {
        _alreadyRequested.AddRange([_anna.Id, _ben.Id]);

        var result = await _sut.SendRequestsAsync(Send(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
        await _participations.DidNotReceive().SaveChangesAsync(_ct);
        await _notifications.DidNotReceive().SendAsync(Arg.Any<SendNotificationRequest>(), _ct);
    }

    [Fact]
    public async Task SendRequests_ExplicitIds_InvitesOnlyThose_DuplicatesOnce_Async()
    {
        var result = await _sut.SendRequestsAsync(Send(_ben.Id, _ben.Id), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal([_ben.Id], _added.Select(x => x.MemberId));
    }

    [Fact]
    public async Task SendRequests_UnknownMemberId_ReturnsMemberNotFound_Async()
    {
        var result = await _sut.SendRequestsAsync(Send(_anna.Id, Guid.NewGuid()), _ct);

        Assert.Equal(ErrorCodes.MemberNotFound, result.Code);
        await _participations.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task SendRequests_InactiveMemberId_ReturnsMemberNotActive_Async()
    {
        _anna.Status = MemberStatus.Inactive;

        var result = await _sut.SendRequestsAsync(Send(_anna.Id), _ct);

        Assert.Equal(ErrorCodes.MemberNotActive, result.Code);
    }

    [Fact]
    public async Task SendRequests_ExplicitIdAlreadyInvited_ReturnsAlreadyRequested_Async()
    {
        _alreadyRequested.Add(_anna.Id);

        var result = await _sut.SendRequestsAsync(Send(_anna.Id, _ben.Id), _ct);

        Assert.Equal(ErrorCodes.EventParticipationAlreadyRequested, result.Code);
        Assert.Empty(_added);
    }

    [Fact]
    public async Task SendRequests_UnknownEvent_ReturnsEventNotFound_Async()
    {
        var result = await _sut.SendRequestsAsync(new SendParticipationRequestsRequest { EventId = Guid.NewGuid() }, _ct);

        Assert.Equal(ErrorCodes.LiturgicalEventNotFound, result.Code);
    }

    [Theory]
    [InlineData(EventStatus.Cancelled, ErrorCodes.LiturgicalEventCancelled)]
    [InlineData(EventStatus.Draft, ErrorCodes.LiturgicalEventNotPublished)]
    public async Task SendRequests_EventNotPublished_ReturnsCode_Async(EventStatus status, string expectedCode)
    {
        _event.Status = status;

        var result = await _sut.SendRequestsAsync(Send(), _ct);

        Assert.Equal(expectedCode, result.Code);
        Assert.Empty(_added);
    }

    [Fact]
    public async Task SendRequests_PastEvent_ReturnsEventAlreadyPassed_Async()
    {
        _event.EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        var result = await _sut.SendRequestsAsync(Send(), _ct);

        Assert.Equal(ErrorCodes.LiturgicalEventAlreadyPassed, result.Code);
    }

    // ---- GetByEvent ----

    [Fact]
    public async Task GetByEvent_CountsEachStatus_Async()
    {
        // Built first: Invitation() configures other substitutes, which NSubstitute forbids inside Returns().
        List<EventParticipation> participations =
        [
            Invitation(_anna, ParticipationStatus.Confirmed),
            Invitation(_ben, ParticipationStatus.Confirmed),
            Invitation(NewMember("Cara"), ParticipationStatus.Declined),
            Invitation(NewMember("Dan"), ParticipationStatus.Unsure),
            Invitation(NewMember("Eve")),
        ];
        _participations.GetByEventAsync(_event.Id, _ct).Returns(participations);

        var summary = (await _sut.GetByEventAsync(_event.Id, _ct)).Value!;

        Assert.Equal(
            (5, 1, 2, 1, 1),
            (summary.TotalCount, summary.InvitedCount, summary.ConfirmedCount, summary.DeclinedCount, summary.UnsureCount));
        Assert.Equal(["Anna", "Ben", "Cara", "Dan", "Eve"], summary.Items.Select(x => x.MemberFullName));
    }

    [Fact]
    public async Task GetByEvent_UnknownEvent_ReturnsEventNotFound_Async()
    {
        var result = await _sut.GetByEventAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.LiturgicalEventNotFound, result.Code);
    }

    // ---- Respond ----

    [Fact]
    public async Task Respond_Invited_StoresAnswerAndNote_Async()
    {
        var participation = Invitation(_anna);

        var result = await _sut.RespondAsync(
            _anna.UserId, _event.Id,
            new RespondParticipationRequest { Status = ParticipationStatus.Declined, Note = "Away" }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(ParticipationStatus.Declined, participation.Status);
        Assert.Equal("Away", participation.Note);
        Assert.NotNull(participation.RespondedAt);
        Assert.Equal("Anna", result.Value!.MemberFullName);
        await _participations.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Respond_UserWithoutMemberProfile_ReturnsMemberNotFound_Async()
    {
        var result = await _sut.RespondAsync(Guid.NewGuid(), _event.Id, Confirm(), _ct);

        Assert.Equal(ErrorCodes.MemberNotFound, result.Code);
    }

    [Fact]
    public async Task Respond_InactiveMember_ReturnsMemberNotActive_Async()
    {
        Invitation(_anna);
        _anna.Status = MemberStatus.Left;

        var result = await _sut.RespondAsync(_anna.UserId, _event.Id, Confirm(), _ct);

        Assert.Equal(ErrorCodes.MemberNotActive, result.Code);
    }

    [Fact]
    public async Task Respond_MemberNotInvited_ReturnsNotInvited_Async()
    {
        // Ben has a profile but only Anna was invited: he must not reach her request.
        Invitation(_anna);
        _members.GetByUserIdAsync(_ben.UserId, _ct).Returns(_ben);

        var result = await _sut.RespondAsync(_ben.UserId, _event.Id, Confirm(), _ct);

        Assert.Equal(ErrorCodes.EventParticipationNotInvited, result.Code);
    }

    [Fact]
    public async Task Respond_SecondAnswer_ReturnsAlreadyResponded_AndKeepsTheFirst_Async()
    {
        var participation = Invitation(_anna, ParticipationStatus.Declined);

        var result = await _sut.RespondAsync(_anna.UserId, _event.Id, Confirm(), _ct);

        Assert.Equal(ErrorCodes.EventParticipationAlreadyResponded, result.Code);
        Assert.Equal(ParticipationStatus.Declined, participation.Status);
        await _participations.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Respond_CancelledEvent_ReturnsEventCancelled_Async()
    {
        Invitation(_anna);
        _event.Status = EventStatus.Cancelled;

        var result = await _sut.RespondAsync(_anna.UserId, _event.Id, Confirm(), _ct);

        Assert.Equal(ErrorCodes.LiturgicalEventCancelled, result.Code);
    }

    [Fact]
    public async Task Respond_PastEvent_ReturnsEventPassed_Async()
    {
        var participation = Invitation(_anna);
        _event.EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        var result = await _sut.RespondAsync(_anna.UserId, _event.Id, Confirm(), _ct);

        Assert.Equal(ErrorCodes.EventParticipationClosed, result.Code);
        Assert.Equal(ParticipationStatus.Invited, participation.Status);
    }

    private static RespondParticipationRequest Confirm() => new() { Status = ParticipationStatus.Confirmed };
}
