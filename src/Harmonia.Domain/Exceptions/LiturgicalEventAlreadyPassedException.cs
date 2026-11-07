using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class LiturgicalEventAlreadyPassedException() : DomainException(ErrorCodes.LiturgicalEventAlreadyPassed, "Liturgical event has already taken place");
