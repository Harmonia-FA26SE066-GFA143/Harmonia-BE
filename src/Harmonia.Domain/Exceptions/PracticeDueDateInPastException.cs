using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class PracticeDueDateInPastException() : DomainException(ErrorCodes.PracticeDueDateInPast, "Practice due date cannot be in the past");
