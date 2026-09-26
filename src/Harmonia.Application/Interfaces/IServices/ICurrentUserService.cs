namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Reads the caller's identity from the current HTTP request's JWT claims.</summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }

    string? RoleName { get; }

    bool IsAuthenticated { get; }

    /// <summary>Remote IP of the caller; behind a reverse proxy this is the proxy's IP until forwarded headers are enabled.</summary>
    string? IpAddress { get; }
}
