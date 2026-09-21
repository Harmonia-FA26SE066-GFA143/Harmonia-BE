using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class ParticipationEventPassedException() : DomainException(ErrorCodes.ParticipationEventPassed, "Event has already taken place");
