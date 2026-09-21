using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class WeekAlreadyPublishedException() : DomainException(ErrorCodes.WeekAlreadyPublished, "Liturgical week has already been published");
