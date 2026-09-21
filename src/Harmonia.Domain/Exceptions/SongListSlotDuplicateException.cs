using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class SongListSlotDuplicateException() : DomainException(ErrorCodes.SongListSlotDuplicate, "Liturgical slot already has a song in this list");
