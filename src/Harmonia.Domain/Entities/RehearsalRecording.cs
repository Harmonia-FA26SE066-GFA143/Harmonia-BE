using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

/// <summary>
/// A take the Choir Director records in the rehearsal practice room (Web B43). Members of the rehearsal's choir can
/// listen to it later. CreatedBy is the director who uploaded it.
/// </summary>
public class RehearsalRecording : BaseAuditableEntity
{
    public Guid RehearsalId { get; set; }

    /// <summary>The song being practised; null for a take of the whole session.</summary>
    public Guid? SongId { get; set; }

    public string? Title { get; set; }

    /// <summary>Private storage id, including the extension; read through a signed URL.</summary>
    public string AudioPublicId { get; set; } = string.Empty;

    /// <summary>Original file name, for display only.</summary>
    public string FileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public int? DurationSeconds { get; set; }

    public Rehearsal Rehearsal { get; set; } = null!;

    public Song? Song { get; set; }
}
