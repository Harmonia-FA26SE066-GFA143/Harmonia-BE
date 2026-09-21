using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class WeekStartNotMondayException() : DomainException(ErrorCodes.WeekStartNotMonday, "Liturgical week must start on a Monday");
