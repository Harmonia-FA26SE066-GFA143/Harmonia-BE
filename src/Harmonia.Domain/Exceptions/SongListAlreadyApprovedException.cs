using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class SongListAlreadyApprovedException() : DomainException(ErrorCodes.SongListAlreadyApproved, "Song list has already been approved");
