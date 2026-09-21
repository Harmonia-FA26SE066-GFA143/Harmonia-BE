using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class EventAlreadyPassedException() : DomainException(ErrorCodes.EventAlreadyPassed, "Liturgical event has already taken place");
