using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class ReviewNotesRequiredException() : DomainException(ErrorCodes.ReviewNotesRequired, "Review notes are required to reject or request a revision");
