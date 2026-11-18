using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class LiturgicalSeasonDateInvalidException() : DomainException(ErrorCodes.LiturgicalSeasonDateInvalid, "Season end date must be after the start date");
