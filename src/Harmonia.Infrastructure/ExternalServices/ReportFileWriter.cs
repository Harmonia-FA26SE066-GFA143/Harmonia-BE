using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Xml.Linq;
using ClosedXML.Excel;
using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Harmonia.Infrastructure.ExternalServices;

/// <summary>
/// CSV, XLSX and PDF are for people, so they carry the column titles and local date formats.
/// XML and JSON are for programs, so they carry the column keys and ISO dates.
/// </summary>
public class ReportFileWriter : IReportFileWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Keeps Vietnamese readable instead of \uXXXX escapes; the output is a file, never inlined in HTML.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = true,
    };

    static ReportFileWriter()
    {
        // Free for individuals and organisations under the revenue limit of the Community licence.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Write(ReportTable table, ReportFormat format) => format switch
    {
        ReportFormat.Csv => WriteCsv(table),
        ReportFormat.Xlsx => WriteXlsx(table),
        ReportFormat.Pdf => WritePdf(table),
        ReportFormat.Xml => WriteXml(table),
        ReportFormat.Json => WriteJson(table),
        _ => throw new NotSupportedException($"Report format '{format}' is not supported."),
    };

    private static byte[] WriteCsv(ReportTable table)
    {
        var csv = new StringBuilder();
        csv.AppendJoin(',', table.Columns.Select(c => CsvCell(c.Title))).Append("\r\n");
        foreach (var row in table.Rows)
        {
            csv.AppendJoin(',', row.Select(value => CsvCell(DisplayText(value)))).Append("\r\n");
        }

        // The BOM makes Excel read the file as UTF-8 instead of the local ANSI code page.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    private static string CsvCell(string text)
    {
        // A spreadsheet runs a cell starting with one of these as a formula; names and titles are user input.
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            text = "'" + text;
        }

        return text.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }

    private static byte[] WriteXlsx(ReportTable table)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Report");

        sheet.Cell(1, 1).Value = table.Title;
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);

        for (var c = 0; c < table.Columns.Count; c++)
        {
            sheet.Cell(3, c + 1).Value = table.Columns[c].Title;
        }

        sheet.Row(3).Style.Font.SetBold();

        for (var r = 0; r < table.Rows.Count; r++)
        {
            for (var c = 0; c < table.Columns.Count; c++)
            {
                // Assigned as typed values, never as formulas, so text starting with "=" stays text.
                sheet.Cell(r + 4, c + 1).Value = table.Rows[r][c] switch
                {
                    null => Blank.Value,
                    int number => number,
                    DateOnly date => date.ToDateTime(TimeOnly.MinValue),
                    DateTime dateTime => dateTime,
                    var other => DisplayText(other),
                };
            }
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] WritePdf(ReportTable table) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(9));

            page.Header().PaddingBottom(10).Text(table.Title).FontSize(14).Bold();

            page.Content().Table(grid =>
            {
                grid.ColumnsDefinition(columns =>
                {
                    foreach (var _ in table.Columns) columns.RelativeColumn();
                });

                grid.Header(header =>
                {
                    foreach (var column in table.Columns)
                    {
                        header.Cell().BorderBottom(1).Padding(3).Text(column.Title).Bold();
                    }
                });

                foreach (var value in table.Rows.SelectMany(row => row))
                {
                    grid.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(DisplayText(value));
                }
            });

            page.Footer().AlignRight().Text(text =>
            {
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        })).GeneratePdf();

    private static byte[] WriteXml(ReportTable table)
    {
        var document = new XDocument(
            new XElement(
                "report",
                new XAttribute("title", table.Title),
                table.Rows.Select(row => new XElement(
                    "row",
                    table.Columns.Select((column, i) => new XElement(column.Key, MachineText(row[i])))))));

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    private static byte[] WriteJson(ReportTable table) =>
        JsonSerializer.SerializeToUtf8Bytes(
            table.Rows.Select(row => table.Columns
                .Select((column, i) => (column.Key, Value: row[i]))
                .ToDictionary(x => x.Key, x => x.Value)),
            JsonOptions);

    private static string DisplayText(object? value) => value switch
    {
        null => string.Empty,
        DateOnly date => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("HH:mm", CultureInfo.InvariantCulture),
        DateTime dateTime => dateTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static string MachineText(object? value) => value switch
    {
        null => string.Empty,
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        DateTime dateTime => dateTime.ToString("s", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
