using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class LiturgicalEventCancelledException() : DomainException(ErrorCodes.LiturgicalEventCancelled, "Liturgical event has been cancelled");
