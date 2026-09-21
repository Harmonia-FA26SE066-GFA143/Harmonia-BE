using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class PracticeSubmissionPastDueException() : DomainException(ErrorCodes.PracticeSubmissionPastDue, "Practice submission is past its due date");
