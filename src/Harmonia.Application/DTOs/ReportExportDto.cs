using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>One report file an Admin exported, with the filters it was made with (UC-34E / FE-53).</summary>
public class ReportExportDto
{
    public Guid Id { get; set; }

    public ReportType ReportType { get; set; }

    public ReportFormat Format { get; set; }

    public Guid? EventId { get; set; }

    public Guid? LiturgicalSeasonId { get; set; }

    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    public Guid ExportedBy { get; set; }

    public string ExportedByName { get; set; } = string.Empty;

    public DateTime ExportedAt { get; set; }

    /// <summary>Short-lived link to the stored file.</summary>
    public string DownloadUrl { get; set; } = string.Empty;
}
