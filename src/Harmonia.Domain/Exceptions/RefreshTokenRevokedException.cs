using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class RefreshTokenRevokedException() : DomainException(ErrorCodes.AuthRefreshTokenRevoked, "Refresh token has been revoked");
