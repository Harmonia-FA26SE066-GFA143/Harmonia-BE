using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class NotificationServiceTests
{
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly INotificationPublisher _publisher = Substitute.For<INotificationPublisher>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();
    private readonly NotificationService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public NotificationServiceTests()
    {
        _sut = new NotificationService(_repository, _publisher, _mapper);
    }

    private static SendNotificationRequest NewRequest(params Guid[] recipients) =>
        new(default(NotificationType), "Title", "Content", recipients);

    [Fact]
    public async Task SendAsync_NoRecipients_StoresAndPublishesNothing_Async()
    {
        await _sut.SendAsync(NewRequest(), _ct);

        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, _ct);
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default!, _ct);
    }

    [Fact]
    public async Task SendAsync_DuplicateRecipients_CreatesOneRowPerUser_Async()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await _sut.SendAsync(NewRequest(userA, userB, userA), _ct);

        await _repository.Received(1).AddAsync(
            Arg.Is<Notification>(n => n.Recipients.Count == 2
                && n.Recipients.Any(r => r.UserId == userA)
                && n.Recipients.Any(r => r.UserId == userB)),
            _ct);
        await _publisher.Received(1).PublishAsync(
            Arg.Any<NotificationDto>(),
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2),
            _ct);
    }

    [Fact]
    public async Task SendAsync_SavesBeforePublishing_Async()
    {
        await _sut.SendAsync(NewRequest(Guid.NewGuid()), _ct);

        Received.InOrder(() =>
        {
            _repository.SaveChangesAsync(_ct);
            _publisher.PublishAsync(Arg.Any<NotificationDto>(), Arg.Any<IReadOnlyCollection<Guid>>(), _ct);
        });
    }

    [Fact]
    public async Task GetForUserAsync_KeepsPagingMetadata_Async()
    {
        var userId = Guid.NewGuid();
        var paging = new PagingRequest { PageNumber = 2, PageSize = 10 };
        _repository.GetForUserAsync(userId, paging, _ct)
            .Returns(new PagedList<NotificationRecipient>([], 2, 10, 25));
        _mapper.Map<List<NotificationDto>>(Arg.Any<object>()).Returns([]);

        var result = await _sut.GetForUserAsync(userId, paging, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.PageNumber);
        Assert.Equal(10, result.Value.PageSize);
        Assert.Equal(25, result.Value.TotalCount);
    }

    [Fact]
    public async Task CountUnreadAsync_ReturnsRepositoryCount_Async()
    {
        var userId = Guid.NewGuid();
        _repository.CountUnreadAsync(userId, _ct).Returns(7);

        var result = await _sut.CountUnreadAsync(userId, _ct);

        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task MarkAsReadAsync_NotOwnedOrMissing_ReturnsNotFound_Async()
    {
        var result = await _sut.MarkAsReadAsync(Guid.NewGuid(), Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.NotificationNotFound, result.Code);
        await _repository.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task MarkAsReadAsync_Unread_SetsReadAndSaves_Async()
    {
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var recipient = new NotificationRecipient { UserId = userId, NotificationId = notificationId };
        _repository.GetRecipientAsync(notificationId, userId, _ct).Returns(recipient);

        var result = await _sut.MarkAsReadAsync(userId, notificationId, _ct);

        Assert.True(result.IsSuccess);
        Assert.True(recipient.IsRead);
        Assert.NotNull(recipient.ReadAt);
        await _repository.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task MarkAsReadAsync_AlreadyRead_KeepsReadAtAndSkipsSave_Async()
    {
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var readAt = DateTime.UtcNow.AddDays(-1);
        var recipient = new NotificationRecipient { IsRead = true, ReadAt = readAt };
        _repository.GetRecipientAsync(notificationId, userId, _ct).Returns(recipient);

        var result = await _sut.MarkAsReadAsync(userId, notificationId, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(readAt, recipient.ReadAt);
        await _repository.DidNotReceive().SaveChangesAsync(_ct);
    }
}
