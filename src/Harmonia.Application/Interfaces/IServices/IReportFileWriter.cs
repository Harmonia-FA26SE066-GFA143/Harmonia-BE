using Harmonia.Application.Common.Models;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Renders a report table as the bytes of a file in the requested format.</summary>
public interface IReportFileWriter
{
    byte[] Write(ReportTable table, ReportFormat format);
}
