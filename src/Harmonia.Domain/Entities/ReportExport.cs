using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class ReportExport : BaseEntity
{
    public ReportType ReportType { get; set; }

    public string Parameters { get; set; } = string.Empty;

    public string FileUrl { get; set; } = string.Empty;

    public Guid ExportedBy { get; set; }

    public DateTime ExportedAt { get; set; }

    public User Exporter { get; set; } = null!;
}
