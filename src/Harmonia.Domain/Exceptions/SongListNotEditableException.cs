using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class SongListNotEditableException() : DomainException(ErrorCodes.SongListNotEditable, "Song list can only be edited while it is a draft");
