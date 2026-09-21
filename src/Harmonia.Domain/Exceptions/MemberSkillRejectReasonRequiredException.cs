using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class MemberSkillRejectReasonRequiredException() : DomainException(ErrorCodes.MemberSkillRejectReasonRequired, "A reason is required to reject a member skill");
