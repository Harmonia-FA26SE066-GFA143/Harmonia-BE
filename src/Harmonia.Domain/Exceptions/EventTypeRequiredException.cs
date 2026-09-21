using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class EventTypeRequiredException() : DomainException(ErrorCodes.EventTypeRequired, "Either a mass type or a ceremony type is required");
