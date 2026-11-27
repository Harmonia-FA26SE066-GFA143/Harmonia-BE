using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class PasswordResetTokenUsedException() : DomainException(ErrorCodes.AuthResetTokenUsed, "Password reset token has already been used");
