using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class PersonnelRequiredCountInvalidException() : DomainException(ErrorCodes.PersonnelRequiredCountInvalid, "Required count must be greater than zero");
