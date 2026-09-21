using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class PracticeTargetRequiredException() : DomainException(ErrorCodes.PracticeTargetRequired, "A skill group or member target is required");
