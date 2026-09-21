using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class DirectorNoteTargetRequiredException() : DomainException(ErrorCodes.DirectorNoteTargetRequired, "Either a week or an event is required");
