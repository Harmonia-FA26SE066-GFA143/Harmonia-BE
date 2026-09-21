using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class SkillInactiveException() : DomainException(ErrorCodes.SkillInactive, "Skill is inactive");
