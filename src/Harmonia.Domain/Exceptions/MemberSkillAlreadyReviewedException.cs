using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class MemberSkillAlreadyReviewedException() : DomainException(ErrorCodes.MemberSkillAlreadyReviewed, "Member skill has already been reviewed");
