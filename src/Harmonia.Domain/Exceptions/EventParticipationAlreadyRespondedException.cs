using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class EventParticipationAlreadyRespondedException() : DomainException(ErrorCodes.EventParticipationAlreadyResponded, "Participation request has already been responded to");
