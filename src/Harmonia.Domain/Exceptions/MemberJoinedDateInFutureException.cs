using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class MemberJoinedDateInFutureException() : DomainException(ErrorCodes.MemberJoinedDateInFuture, "Member joined date cannot be in the future");
