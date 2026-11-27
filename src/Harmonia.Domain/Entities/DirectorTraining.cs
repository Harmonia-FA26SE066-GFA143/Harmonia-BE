using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

/// <summary>
/// One course a nominated member is sent to, usually run by the diocese. The system only records it: no class
/// schedule or attendance. A nomination keeps every course, so retakes and basic → advanced stay in its history.
/// </summary>
public class DirectorTraining : BaseAuditableEntity
{
    public Guid NominationId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    /// <summary>Who runs the course, e.g. the diocesan sacred music board.</summary>
    public string? Organizer { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DirectorTrainingResult Result { get; set; }

    public string? ResultNote { get; set; }

    public DateTime? ResultRecordedAt { get; set; }

    /// <summary>Private file; read through a signed URL.</summary>
    public string? CertificatePublicId { get; set; }

    public string? CertificateFileName { get; set; }

    public long? CertificateSizeBytes { get; set; }

    public DirectorNomination Nomination { get; set; } = null!;
}
