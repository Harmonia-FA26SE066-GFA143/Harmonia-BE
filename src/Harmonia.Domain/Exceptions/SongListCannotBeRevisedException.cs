using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class SongListCannotBeRevisedException() : DomainException(ErrorCodes.SongListCannotBeRevised, "A new version can only be created from a rejected or revision-requested song list");
