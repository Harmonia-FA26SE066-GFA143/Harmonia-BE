using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class RehearsalTimeInvalidException() : DomainException(ErrorCodes.RehearsalTimeInvalid, "Rehearsal end time must be after the start time");
