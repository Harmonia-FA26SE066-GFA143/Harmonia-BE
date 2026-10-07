using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.API.Tests;

public class NotificationsEndpointsTests(HarmoniaApiFactory factory) : IClassFixture<HarmoniaApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private Task<Guid> SeedNotificationAsync(
        string recipientEmail, string title, CancellationToken cancellationToken = default) =>
        factory.WithDbAsync(
            async (db, ct) =>
            {
                var userId = await db.Users.Where(u => u.Email == recipientEmail).Select(u => u.Id).SingleAsync(ct);
                var notification = new Notification
                {
                    Id = Guid.NewGuid(), Title = title, Content = "c", CreatedAt = DateTime.Now,
                    Recipients = [new NotificationRecipient { Id = Guid.NewGuid(), UserId = userId }],
                };
                db.Notifications.Add(notification);
                await db.SaveChangesAsync(ct);
                return notification.Id;
            },
            cancellationToken);

    private static async Task<int> UnreadCountAsync(HttpClient client, CancellationToken cancellationToken = default) =>
        await client.GetFromJsonAsync<int>("api/notifications/unread-count", cancellationToken);

    [Fact]
    public async Task MarkAsRead_OwnNotification_Returns204AndDecrementsUnread_Async()
    {
        var user = await factory.AddUserAsync("notif-owner@test.com", cancellationToken: _ct);
        var id = await SeedNotificationAsync(user.Email, "Rehearsal moved", _ct);
        var client = await factory.CreateClientAsAsync(user.Email, _ct);
        var before = await UnreadCountAsync(client, _ct);

        var response = await client.PutAsync($"api/notifications/{id}/read", null, _ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(before - 1, await UnreadCountAsync(client, _ct));
    }

    [Fact]
    public async Task MarkAsRead_AnotherUsersNotification_Returns404Not403_Async()
    {
        var id = await SeedNotificationAsync("member@test.com", "Private", _ct);
        var stranger = await factory.CreateClientAsAsync("member2@test.com", _ct);

        var response = await stranger.PutAsync($"api/notifications/{id}/read", null, _ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ErrorResponse>(TestJson.Options, _ct))!;
        Assert.Equal(ErrorCodes.NotificationNotFound, error.Code);
    }

    [Fact]
    public async Task MarkAsRead_NonGuidId_Returns404FromRouting_Async()
    {
        var client = await factory.CreateClientAsAsync("member@test.com", _ct);

        var response = await client.PutAsync("api/notifications/not-a-guid/read", null, _ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyOwnNotificationsWithPaging_Async()
    {
        var user = await factory.AddUserAsync("notif-list@test.com", cancellationToken: _ct);
        await SeedNotificationAsync(user.Email, "Mine 1", _ct);
        await SeedNotificationAsync(user.Email, "Mine 2", _ct);
        await SeedNotificationAsync("member@test.com", "Someone else's", _ct);
        var client = await factory.CreateClientAsAsync(user.Email, _ct);

        var json = await client.GetFromJsonAsync<JsonElement>("api/notifications?pageNumber=1&pageSize=1", _ct);

        Assert.Equal(2, json.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, json.GetProperty("pageSize").GetInt32());
        Assert.True(json.GetProperty("hasNextPage").GetBoolean());
        var title = json.GetProperty("items")[0].GetProperty("title").GetString();
        Assert.StartsWith("Mine", title);
    }

    [Fact]
    public async Task GetMine_OversizedPage_IsClamped_Async()
    {
        var client = await factory.CreateClientAsAsync("member@test.com", _ct);

        var json = await client.GetFromJsonAsync<JsonElement>("api/notifications?pageSize=5000", _ct);

        Assert.Equal(100, json.GetProperty("pageSize").GetInt32());
    }
}
