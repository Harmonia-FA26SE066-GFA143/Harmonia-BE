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

public class SongListServiceTests
{
    private readonly ISongListRepository _songLists = Substitute.For<ISongListRepository>();
    private readonly ILiturgicalEventRepository _events = Substitute.For<ILiturgicalEventRepository>();
    private readonly IGenericRepository<Song> _songs = Substitute.For<IGenericRepository<Song>>();
    private readonly IGenericRepository<LiturgicalSlot> _slots = Substitute.For<IGenericRepository<LiturgicalSlot>>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly SongListService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _directorId = Guid.NewGuid();
    private readonly Guid _priestId = Guid.NewGuid();
    private readonly Guid _songId = Guid.NewGuid();
    private readonly Guid _slotId = Guid.NewGuid();
    private readonly LiturgicalEvent _event;
    private readonly List<SongListItem> _addedItems = [];

    public SongListServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<SongListProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new SongListService(_songLists, _events, _songs, _slots, _users, _notifications, mapper);

        _event = new LiturgicalEvent { Id = _eventId, Status = EventStatus.Published };
        _events.GetByIdAsync(_eventId, _ct).Returns(_event);
        _songs.GetByIdAsync(_songId, _ct).Returns(new Song { Id = _songId, IsActive = true });
        _slots.GetByIdAsync(_slotId, _ct).Returns(new LiturgicalSlot { Id = _slotId, IsActive = true });
        _songLists.AddItemsAsync(Arg.Do<IEnumerable<SongListItem>>(items => _addedItems.AddRange(items)), _ct)
            .Returns(Task.CompletedTask);
        _users.GetActiveUserIdsByRolesAsync(
                Arg.Is<IReadOnlyCollection<string>>(r => r.SequenceEqual(new[] { RoleNames.ParishPriest })), _ct)
            .Returns([_priestId]);
    }

    private CreateSongListRequest NewCreateRequest() => new() { EventId = _eventId, Items = [NewItem()] };

    private UpdateSongListItemRequest NewItem() => new() { SongId = _songId, SlotId = _slotId, DisplayOrder = 1 };

    /// <summary>Registers a list as both the row with this id and the latest version of its event.</summary>
    private SongList Existing(SongListStatus status, int version = 1, bool hasItems = true)
    {
        var songList = new SongList
        {
            Id = Guid.NewGuid(), EventId = _eventId, Version = version, Status = status, ProposedBy = _directorId,
        };
        if (hasItems)
        {
            songList.Items.Add(new SongListItem { SongListId = songList.Id, SongId = _songId, SlotId = _slotId });
        }

        _songLists.GetByIdAsync(songList.Id, _ct).Returns(songList);
        _songLists.GetByIdWithDetailsAsync(songList.Id, _ct).Returns(songList);
        _songLists.GetLatestVersionAsync(_eventId, _ct).Returns(songList);
        return songList;
    }

    // ---- Read ----

    [Fact]
    public async Task GetApprovedForEvent_PublishedEvent_ReturnsList_Async()
    {
        var approved = new SongList { Id = Guid.NewGuid(), EventId = _eventId, Status = SongListStatus.Approved };
        _songLists.GetApprovedForEventAsync(_eventId, _ct).Returns(approved);

        var result = await _sut.GetApprovedForEventAsync(_eventId, _ct);

        Assert.Equal(approved.Id, result.Value!.Id);
    }

    [Theory]
    [InlineData(EventStatus.Cancelled)]
    [InlineData(EventStatus.Draft)]
    public async Task GetApprovedForEvent_EventNotPublished_ReturnsNotFound_Async(EventStatus status)
    {
        _event.Status = status;
        _songLists.GetApprovedForEventAsync(_eventId, _ct)
            .Returns(new SongList { Id = Guid.NewGuid(), EventId = _eventId, Status = SongListStatus.Approved });

        var result = await _sut.GetApprovedForEventAsync(_eventId, _ct);

        Assert.Equal(ErrorCodes.SongListNotFound, result.Code);
    }

    [Fact]
    public async Task GetApprovedForEvent_EventMissing_ReturnsNotFound_Async()
    {
        var result = await _sut.GetApprovedForEventAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.SongListNotFound, result.Code);
    }

    [Fact]
    public async Task GetApprovedForEvent_None_ReturnsNotFound_Async()
    {
        var result = await _sut.GetApprovedForEventAsync(_eventId, _ct);

        Assert.Equal(ErrorCodes.SongListNotFound, result.Code);
    }

    [Fact]
    public async Task GetById_Missing_ReturnsNotFound_Async()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.SongListNotFound, result.Code);
    }

    [Fact]
    public async Task GetPending_AsksForSubmittedLists_Async()
    {
        _songLists.GetByStatusAsync(SongListStatus.Submitted, _ct)
            .Returns([new SongList { Id = Guid.NewGuid(), Status = SongListStatus.Submitted }]);

        var result = await _sut.GetPendingAsync(_ct);

        Assert.Equal(SongListStatus.Submitted, Assert.Single(result.Value!).Status);
    }

    // ---- Create ----

    [Fact]
    public async Task Create_FirstVersion_StoresDraftWithItems_Async()
    {
        SongList? added = null;
        _songLists.AddAsync(Arg.Do<SongList>(s => added = s), _ct).Returns(Task.CompletedTask);
        _songLists.GetByIdWithDetailsAsync(Arg.Any<Guid>(), _ct).Returns(_ => added);

        var result = await _sut.CreateAsync(NewCreateRequest(), _directorId, _ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(1, added.Version);
        Assert.Equal(SongListStatus.Draft, added.Status);
        Assert.Equal(_directorId, added.ProposedBy);
        Assert.Null(added.PreviousVersionId);
        var item = Assert.Single(added.Items);
        Assert.Equal(_songId, item.SongId);
        Assert.Equal(_slotId, item.SlotId);
        // List and items go in one save, so a failure cannot leave an empty draft behind.
        await _songLists.Received(1).SaveChangesAsync(_ct);
        await _songLists.DidNotReceiveWithAnyArgs().AddItemsAsync(default!, default);
    }

    [Theory]
    [InlineData(SongListStatus.Rejected)]
    [InlineData(SongListStatus.NeedsRevision)]
    public async Task Create_AfterRejectedOrNeedsRevision_StoresNextVersionLinkedToPrevious_Async(SongListStatus previousStatus)
    {
        var previous = Existing(previousStatus, version: 2);
        SongList? added = null;
        _songLists.AddAsync(Arg.Do<SongList>(s => added = s), _ct).Returns(Task.CompletedTask);

        var result = await _sut.CreateAsync(NewCreateRequest(), _directorId, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, added!.Version);
        Assert.Equal(previous.Id, added.PreviousVersionId);
    }

    [Theory]
    [InlineData(SongListStatus.Approved, ErrorCodes.SongListEventHasApprovedVersion)]
    [InlineData(SongListStatus.Draft, ErrorCodes.SongListCannotBeRevised)]
    [InlineData(SongListStatus.Submitted, ErrorCodes.SongListCannotBeRevised)]
    public async Task Create_LatestVersionStillOpenOrApproved_ReturnsCode_Async(SongListStatus latestStatus, string expectedCode)
    {
        Existing(latestStatus);

        var result = await _sut.CreateAsync(NewCreateRequest(), _directorId, _ct);

        Assert.Equal(expectedCode, result.Code);
        await _songLists.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Create_EventMissing_ReturnsEventNotFound_Async()
    {
        var request = NewCreateRequest();
        request.EventId = Guid.NewGuid();

        var result = await _sut.CreateAsync(request, _directorId, _ct);

        Assert.Equal(ErrorCodes.EventNotFound, result.Code);
    }

    [Theory]
    [InlineData(EventStatus.Cancelled, ErrorCodes.EventCancelled)]
    [InlineData(EventStatus.Draft, ErrorCodes.EventNotPublished)]
    public async Task Create_EventNotPublished_ReturnsCode_Async(EventStatus status, string expectedCode)
    {
        _event.Status = status;

        var result = await _sut.CreateAsync(NewCreateRequest(), _directorId, _ct);

        Assert.Equal(expectedCode, result.Code);
    }

    [Fact]
    public async Task Create_SongMissing_ReturnsSongNotFound_Async()
    {
        var request = NewCreateRequest();
        request.Items[0].SongId = Guid.NewGuid();

        var result = await _sut.CreateAsync(request, _directorId, _ct);

        Assert.Equal(ErrorCodes.SongNotFound, result.Code);
        await _songLists.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Create_SongInactive_ReturnsSongInactive_Async()
    {
        _songs.GetByIdAsync(_songId, _ct).Returns(new Song { Id = _songId, IsActive = false });

        var result = await _sut.CreateAsync(NewCreateRequest(), _directorId, _ct);

        Assert.Equal(ErrorCodes.SongInactive, result.Code);
    }

    [Fact]
    public async Task Create_SlotMissing_ReturnsSlotNotFound_Async()
    {
        var request = NewCreateRequest();
        request.Items[0].SlotId = Guid.NewGuid();

        var result = await _sut.CreateAsync(request, _directorId, _ct);

        Assert.Equal(ErrorCodes.SlotNotFound, result.Code);
    }

    [Fact]
    public async Task Create_SlotInactive_ReturnsLookupInactive_Async()
    {
        _slots.GetByIdAsync(_slotId, _ct).Returns(new LiturgicalSlot { Id = _slotId, IsActive = false });

        var result = await _sut.CreateAsync(NewCreateRequest(), _directorId, _ct);

        Assert.Equal(ErrorCodes.LookupInactive, result.Code);
    }

    // ---- UpdateItems ----

    [Fact]
    public async Task UpdateItems_Draft_ReplacesItems_Async()
    {
        var songList = Existing(SongListStatus.Draft);
        List<SongListItem> old = [new SongListItem { SongListId = songList.Id }];
        _songLists.GetItemsAsync(songList.Id, _ct).Returns(old);

        var result = await _sut.UpdateItemsAsync(songList.Id, new UpdateSongListItemsRequest { Items = [NewItem()] }, _ct);

        Assert.True(result.IsSuccess);
        await _songLists.Received(1).RemoveItemsAsync(old, _ct);
        Assert.Equal(songList.Id, Assert.Single(_addedItems).SongListId);
        await _songLists.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task UpdateItems_Missing_ReturnsNotFound_Async()
    {
        var result = await _sut.UpdateItemsAsync(Guid.NewGuid(), new UpdateSongListItemsRequest { Items = [NewItem()] }, _ct);

        Assert.Equal(ErrorCodes.SongListNotFound, result.Code);
    }

    [Fact]
    public async Task UpdateItems_OlderVersion_ReturnsNotLatestVersion_Async()
    {
        var older = Existing(SongListStatus.Rejected);
        Existing(SongListStatus.Draft, version: 2);

        var result = await _sut.UpdateItemsAsync(older.Id, new UpdateSongListItemsRequest { Items = [NewItem()] }, _ct);

        Assert.Equal(ErrorCodes.SongListNotLatestVersion, result.Code);
    }

    [Fact]
    public async Task UpdateItems_NotDraft_ReturnsNotEditable_Async()
    {
        var songList = Existing(SongListStatus.Submitted);

        var result = await _sut.UpdateItemsAsync(songList.Id, new UpdateSongListItemsRequest { Items = [NewItem()] }, _ct);

        Assert.Equal(ErrorCodes.SongListNotEditable, result.Code);
        await _songLists.DidNotReceiveWithAnyArgs().RemoveItemsAsync(default!, default);
    }

    // ---- Submit ----

    [Fact]
    public async Task Submit_Draft_MarksSubmitted_AndNotifiesParishPriests_Async()
    {
        var songList = Existing(SongListStatus.Draft, version: 2);

        var result = await _sut.SubmitAsync(songList.Id, _ct);

        Assert.Equal(SongListStatus.Submitted, result.Value!.Status);
        Assert.NotNull(songList.SubmittedAt);
        await _songLists.Received(1).SaveChangesAsync(_ct);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(r => r.Type == NotificationType.SongListSubmitted
                && r.RecipientUserIds.SequenceEqual(new[] { _priestId })
                && r.Content.Contains("version 2")
                && r.ReferenceType == nameof(SongList) && r.ReferenceId == songList.Id),
            _ct);
    }

    [Fact]
    public async Task Submit_NoActiveParishPriest_StillSubmits_WithoutNotification_Async()
    {
        _users.GetActiveUserIdsByRolesAsync(Arg.Any<IReadOnlyCollection<string>>(), _ct).Returns([]);
        var songList = Existing(SongListStatus.Draft);

        var result = await _sut.SubmitAsync(songList.Id, _ct);

        Assert.True(result.IsSuccess);
        await _notifications.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Theory]
    [InlineData(SongListStatus.Submitted, ErrorCodes.SongListAlreadySubmitted)]
    [InlineData(SongListStatus.Approved, ErrorCodes.SongListNotEditable)]
    [InlineData(SongListStatus.Rejected, ErrorCodes.SongListNotEditable)]
    public async Task Submit_NotDraft_ReturnsCode_Async(SongListStatus status, string expectedCode)
    {
        var songList = Existing(status);

        var result = await _sut.SubmitAsync(songList.Id, _ct);

        Assert.Equal(expectedCode, result.Code);
        await _notifications.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Fact]
    public async Task Submit_NoItems_ReturnsEmpty_Async()
    {
        var songList = Existing(SongListStatus.Draft, hasItems: false);

        var result = await _sut.SubmitAsync(songList.Id, _ct);

        Assert.Equal(ErrorCodes.SongListEmpty, result.Code);
        Assert.Equal(SongListStatus.Draft, songList.Status);
    }

    [Fact]
    public async Task Submit_OlderVersion_ReturnsNotLatestVersion_Async()
    {
        var older = Existing(SongListStatus.Draft);
        Existing(SongListStatus.Draft, version: 2);

        var result = await _sut.SubmitAsync(older.Id, _ct);

        Assert.Equal(ErrorCodes.SongListNotLatestVersion, result.Code);
    }

    // ---- Review ----

    [Theory]
    [InlineData(ReviewDecision.Approve, SongListStatus.Approved, "approved")]
    [InlineData(ReviewDecision.Reject, SongListStatus.Rejected, "rejected")]
    [InlineData(ReviewDecision.RequestRevision, SongListStatus.NeedsRevision, "needs revision")]
    public async Task Review_Submitted_SetsStatus_StoresReview_AndNotifiesProposer_Async(
        ReviewDecision decision, SongListStatus expectedStatus, string expectedWording)
    {
        var songList = Existing(SongListStatus.Submitted);
        SongListReview? review = null;
        _songLists.AddReviewAsync(Arg.Do<SongListReview>(r => review = r), _ct).Returns(Task.CompletedTask);

        var result = await _sut.ReviewAsync(
            songList.Id, new ReviewSongListRequest { Decision = decision, Notes = "note" }, _priestId, _ct);

        Assert.Equal(expectedStatus, result.Value!.Status);
        Assert.NotNull(songList.DecidedAt);
        Assert.Equal(songList.Id, review!.SongListId);
        Assert.Equal(_priestId, review.ReviewerId);
        Assert.Equal(decision, review.Decision);
        Assert.Equal("note", review.Notes);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(r => r.Type == NotificationType.SongListDecision
                && r.RecipientUserIds.SequenceEqual(new[] { _directorId })
                && r.Content.Contains(expectedWording)
                && r.ReferenceId == songList.Id),
            _ct);
    }

    [Theory]
    [InlineData(SongListStatus.Draft)]
    [InlineData(SongListStatus.Approved)]
    public async Task Review_NotSubmitted_ReturnsNotSubmitted_Async(SongListStatus status)
    {
        var songList = Existing(status);

        var result = await _sut.ReviewAsync(
            songList.Id, new ReviewSongListRequest { Decision = ReviewDecision.Approve }, _priestId, _ct);

        Assert.Equal(ErrorCodes.SongListNotSubmitted, result.Code);
        Assert.Equal(status, songList.Status);
        await _songLists.DidNotReceiveWithAnyArgs().AddReviewAsync(default!, default);
    }

    [Fact]
    public async Task Review_Missing_ReturnsNotFound_Async()
    {
        var result = await _sut.ReviewAsync(
            Guid.NewGuid(), new ReviewSongListRequest { Decision = ReviewDecision.Approve }, _priestId, _ct);

        Assert.Equal(ErrorCodes.SongListNotFound, result.Code);
    }
}
