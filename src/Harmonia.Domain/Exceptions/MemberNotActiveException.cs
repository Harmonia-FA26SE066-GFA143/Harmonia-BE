using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class MemberNotActiveException() : DomainException(ErrorCodes.MemberNotActive, "Member is not active");
