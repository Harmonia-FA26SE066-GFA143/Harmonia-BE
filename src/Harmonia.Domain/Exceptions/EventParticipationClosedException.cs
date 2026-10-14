using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class EventParticipationClosedException() : DomainException(ErrorCodes.EventParticipationClosed, "Event has already taken place");
