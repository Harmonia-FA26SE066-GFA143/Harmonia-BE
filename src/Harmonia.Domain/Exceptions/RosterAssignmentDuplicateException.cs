using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class RosterAssignmentDuplicateException() : DomainException(ErrorCodes.RosterAssignmentDuplicate, "Member is already assigned to this position");
