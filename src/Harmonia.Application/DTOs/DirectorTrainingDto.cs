using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class DirectorTrainingDto
{
    public Guid Id { get; set; }

    public Guid NominationId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public string? Organizer { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DirectorTrainingResult Result { get; set; }

    public string? ResultNote { get; set; }

    public DateTime? ResultRecordedAt { get; set; }

    public string? CertificateFileName { get; set; }

    public long? CertificateSizeBytes { get; set; }

    /// <summary>Short-lived signed URL of the private certificate; null when none was uploaded.</summary>
    public string? CertificateUrl { get; set; }

    public DateTime CreatedAt { get; set; }
}
