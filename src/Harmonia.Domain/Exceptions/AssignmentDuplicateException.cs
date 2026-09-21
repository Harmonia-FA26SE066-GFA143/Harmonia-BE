using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class AssignmentDuplicateException() : DomainException(ErrorCodes.AssignmentDuplicate, "Member is already assigned to this position");
