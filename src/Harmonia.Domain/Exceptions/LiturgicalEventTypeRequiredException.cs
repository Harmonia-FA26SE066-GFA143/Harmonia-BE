using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class LiturgicalEventTypeRequiredException() : DomainException(ErrorCodes.LiturgicalEventTypeRequired, "Either a mass type or a ceremony type is required");
