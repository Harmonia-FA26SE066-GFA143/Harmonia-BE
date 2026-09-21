using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class ParticipationAlreadyRespondedException() : DomainException(ErrorCodes.ParticipationAlreadyResponded, "Participation request has already been responded to");
