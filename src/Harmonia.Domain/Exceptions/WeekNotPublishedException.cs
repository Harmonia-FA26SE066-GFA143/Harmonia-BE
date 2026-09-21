using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class WeekNotPublishedException() : DomainException(ErrorCodes.WeekNotPublished, "Liturgical week has not been published");
