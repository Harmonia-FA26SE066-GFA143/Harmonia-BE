using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class EventDateOutsideWeekException() : DomainException(ErrorCodes.EventDateOutsideWeek, "Event date is outside the liturgical week");
