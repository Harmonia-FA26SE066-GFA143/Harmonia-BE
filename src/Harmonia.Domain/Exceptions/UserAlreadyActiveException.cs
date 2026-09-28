using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class UserAlreadyActiveException() : DomainException(ErrorCodes.UserAlreadyActive, "User account is already active");
