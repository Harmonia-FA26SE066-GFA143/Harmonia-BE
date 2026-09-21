using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class LookupInactiveException() : DomainException(ErrorCodes.LookupInactive, "Lookup item is inactive");
