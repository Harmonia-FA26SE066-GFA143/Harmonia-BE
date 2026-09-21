using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class SongListEmptyException() : DomainException(ErrorCodes.SongListEmpty, "Song list must contain at least one song");
