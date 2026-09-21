using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class RefreshTokenExpiredException() : DomainException(ErrorCodes.AuthRefreshTokenExpired, "Refresh token has expired");
