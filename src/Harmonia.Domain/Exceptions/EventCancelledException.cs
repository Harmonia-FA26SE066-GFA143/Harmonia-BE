using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class EventCancelledException() : DomainException(ErrorCodes.EventCancelled, "Liturgical event has been cancelled");
