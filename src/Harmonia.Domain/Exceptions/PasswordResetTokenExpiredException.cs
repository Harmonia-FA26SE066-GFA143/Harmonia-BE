using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class PasswordResetTokenExpiredException() : DomainException(ErrorCodes.AuthResetTokenExpired, "Password reset token has expired");
