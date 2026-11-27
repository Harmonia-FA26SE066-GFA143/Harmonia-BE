namespace Harmonia.Application.Common.Models;

/// <summary>
/// A report flattened for export. Each column has a <c>Key</c> for machine formats (XML, JSON) and a
/// <c>Title</c> shown to people (CSV, XLSX, PDF); every row holds one value per column, in column order.
/// </summary>
public record ReportTable(
    string Title,
    IReadOnlyList<(string Key, string Title)> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows);
