using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Harmonia.API.Hubs;

/// <summary>
/// Server to client only: the client connects and listens, it never invokes anything here,
/// which is why the hub has no methods. Recipients are addressed by user id: SignalR's
/// DefaultUserIdProvider reads the same NameIdentifier claim the access token carries.
/// </summary>
[Authorize]
public class NotificationHub : Hub<INotificationClient>
{
    public const string Route = "/hubs/notifications";
}
