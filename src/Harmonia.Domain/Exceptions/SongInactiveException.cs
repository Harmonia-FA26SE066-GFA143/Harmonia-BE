using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class SongInactiveException() : DomainException(ErrorCodes.SongInactive, "Song is inactive");
