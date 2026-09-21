using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class SongListNotSubmittedException() : DomainException(ErrorCodes.SongListNotSubmitted, "Song list has not been submitted");
