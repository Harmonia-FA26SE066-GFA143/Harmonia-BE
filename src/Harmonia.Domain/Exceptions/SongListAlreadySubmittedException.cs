using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class SongListAlreadySubmittedException() : DomainException(ErrorCodes.SongListAlreadySubmitted, "Song list has already been submitted");
