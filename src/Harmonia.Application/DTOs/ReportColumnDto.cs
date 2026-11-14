namespace Harmonia.Application.DTOs;

/// <summary>
/// One column of a report: <see cref="Key"/> is the JSON field name of the report rows, <see cref="Title"/> the
/// Vietnamese heading the exported file uses, so the screen and the file show the same words.
/// </summary>
public class ReportColumnDto
{
    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
}
