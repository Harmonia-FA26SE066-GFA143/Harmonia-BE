using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class UserAlreadyInactiveException() : DomainException(ErrorCodes.UserAlreadyInactive, "User account is already inactive");
