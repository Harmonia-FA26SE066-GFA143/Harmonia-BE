using Harmonia.API.Hubs;
using Harmonia.API.Services;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Harmonia.API.Tests;

public class SignalRNotificationPublisherTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly IHubContext<NotificationHub, INotificationClient> _hub =
        Substitute.For<IHubContext<NotificationHub, INotificationClient>>();
    private readonly INotificationClient _client = Substitute.For<INotificationClient>();
    private readonly SignalRNotificationPublisher _sut;
    private readonly NotificationDto _notification = new() { Type = NotificationType.AssignmentNotice };
    private readonly Guid[] _userIds = [Guid.NewGuid(), Guid.NewGuid()];

    public SignalRNotificationPublisherTests()
    {
        _hub.Clients.Users(Arg.Any<IReadOnlyList<string>>()).Returns(_client);
        _sut = new SignalRNotificationPublisher(_hub, NullLogger<SignalRNotificationPublisher>.Instance);
    }

    [Fact]
    public async Task PublishAsync_SendsToEachRecipientByUserId_Async()
    {
        await _sut.PublishAsync(_notification, _userIds, _ct);

        _hub.Clients.Received(1).Users(Arg.Is<IReadOnlyList<string>>(ids =>
            ids.SequenceEqual(_userIds.Select(id => id.ToString()))));
        await _client.Received(1).ReceiveNotificationAsync(_notification, _ct);
    }

    [Fact]
    public async Task PublishAsync_TransportFails_DoesNotThrow_Async()
    {
        _client.ReceiveNotificationAsync(_notification, _ct).ThrowsAsync(new IOException("backplane down"));

        await _sut.PublishAsync(_notification, _userIds, _ct);
    }

    [Fact]
    public async Task PublishAsync_RequestAborted_DoesNotThrow_Async()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        _client.ReceiveNotificationAsync(_notification, aborted.Token).ThrowsAsync(new OperationCanceledException(aborted.Token));

        await _sut.PublishAsync(_notification, _userIds, aborted.Token);
    }
}
