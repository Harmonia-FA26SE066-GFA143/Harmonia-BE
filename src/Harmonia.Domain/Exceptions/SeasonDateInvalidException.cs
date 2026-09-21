using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class SeasonDateInvalidException() : DomainException(ErrorCodes.SeasonDateInvalid, "Season end date must be after the start date");
