using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class PracticeSubmissionAlreadyReviewedException() : DomainException(ErrorCodes.PracticeSubmissionAlreadyReviewed, "Practice submission has already been reviewed");
