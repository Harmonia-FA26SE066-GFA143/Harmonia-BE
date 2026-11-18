using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ClosedXML.Excel;
using Harmonia.Application.Common.Models;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.ExternalServices;

namespace Harmonia.Infrastructure.Tests;

public class ReportFileWriterTests
{
    private readonly ReportFileWriter _sut = new();

    private readonly ReportTable _table = new(
        "Lịch sử phục vụ",
        [("fullName", "Họ tên"), ("eventDate", "Ngày"), ("songCount", "Số bài hát"), ("note", "Ghi chú")],
        [
            ["Nguyễn Thị Ánh", new DateOnly(2026, 10, 4), 5, null],
            ["=SUM(A1)", new DateOnly(2026, 9, 27), 12, "Has, comma and \"quote\""],
        ]);

    [Fact]
    public void Csv_HasBomTitlesAndQuoting_AndDefusesFormulas()
    {
        var bytes = _sut.Write(_table, ReportFormat.Csv);

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        Assert.Equal(
            [
                "Họ tên,Ngày,Số bài hát,Ghi chú",
                "Nguyễn Thị Ánh,04/10/2026,5,",
                "'=SUM(A1),27/09/2026,12,\"Has, comma and \"\"quote\"\"\"",
                "",
            ],
            Encoding.UTF8.GetString(bytes[3..]).Split("\r\n"));
    }

    [Fact]
    public void Xlsx_KeepsTypes_AndNeverTurnsTextIntoAFormula()
    {
        using var workbook = new XLWorkbook(new MemoryStream(_sut.Write(_table, ReportFormat.Xlsx)));
        var sheet = workbook.Worksheet(1);

        Assert.Equal("Lịch sử phục vụ", sheet.Cell(1, 1).GetString());
        Assert.Equal(["Họ tên", "Ngày", "Số bài hát", "Ghi chú"], sheet.Row(3).CellsUsed().Select(c => c.GetString()));
        Assert.Equal(new DateTime(2026, 10, 4), sheet.Cell(4, 2).GetDateTime());
        Assert.Equal(5, sheet.Cell(4, 3).GetValue<int>());
        Assert.True(sheet.Cell(4, 4).IsEmpty());
        Assert.Equal("=SUM(A1)", sheet.Cell(5, 1).GetString());
        Assert.False(sheet.Cell(5, 1).HasFormula);
    }

    [Fact]
    public void Pdf_RendersVietnameseWithTheBundledFont()
    {
        // Makes QuestPDF throw when the font lacks a glyph instead of printing an empty box.
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = true;

        var bytes = _sut.Write(_table, ReportFormat.Pdf);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes[..4]));
    }

    [Fact]
    public void Xml_UsesKeysAndIsoDates()
    {
        var report = XDocument.Load(new MemoryStream(_sut.Write(_table, ReportFormat.Xml))).Root!;

        Assert.Equal(("report", "Lịch sử phục vụ"), (report.Name.LocalName, report.Attribute("title")!.Value));
        var first = report.Elements("row").First();
        Assert.Equal(
            ("Nguyễn Thị Ánh", "2026-10-04", "5", ""),
            (first.Element("fullName")!.Value, first.Element("eventDate")!.Value, first.Element("songCount")!.Value,
                first.Element("note")!.Value));
        Assert.Equal(2, report.Elements("row").Count());
    }

    [Fact]
    public void Json_UsesKeysAndTypedValues_WithReadableVietnamese()
    {
        var bytes = _sut.Write(_table, ReportFormat.Json);

        Assert.Contains("Nguyễn Thị Ánh", Encoding.UTF8.GetString(bytes));
        var first = JsonDocument.Parse(bytes).RootElement[0];
        Assert.Equal("2026-10-04", first.GetProperty("eventDate").GetString());
        Assert.Equal(5, first.GetProperty("songCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("note").ValueKind);
    }
}
