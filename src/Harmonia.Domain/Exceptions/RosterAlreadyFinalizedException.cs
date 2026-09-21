using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class RosterAlreadyFinalizedException() : DomainException(ErrorCodes.RosterAlreadyFinalized, "Service roster has already been finalized");
