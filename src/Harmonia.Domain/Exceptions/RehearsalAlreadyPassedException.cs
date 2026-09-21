using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class RehearsalAlreadyPassedException() : DomainException(ErrorCodes.RehearsalAlreadyPassed, "Rehearsal has already taken place");
