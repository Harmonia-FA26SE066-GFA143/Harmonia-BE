using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class ReportExportServiceTests
{
    private const string StoredId = "harmonia/report-exports/stored.csv";

    private readonly IReportService _reports = Substitute.For<IReportService>();
    private readonly IReportFileWriter _writer = Substitute.For<IReportFileWriter>();
    private readonly IFileStorageService _storage = Substitute.For<IFileStorageService>();
    private readonly IReportExportRepository _exports = Substitute.For<IReportExportRepository>();
    private readonly ReportExportService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly ExportReportRequest _request = new()
    {
        Type = ReportType.RehearsalAttendance, Format = ReportFormat.Csv, EventId = Guid.NewGuid(),
    };

    private ReportTable? _table;

    public ReportExportServiceTests()
    {
        _sut = new ReportExportService(_reports, _writer, _storage, _exports);
        _writer.Write(Arg.Do<ReportTable>(t => _table = t), ReportFormat.Csv).Returns([1, 2, 3]);
        _storage.UploadAsync(default!, default!, default!, default, default)
            .ReturnsForAnyArgs(Result<FileUploadResponse>.Success(new FileUploadResponse(StoredId, string.Empty)));
    }

    private void ReturnsAttendancePages(int totalCount, params string[][] pages) =>
        _reports.GetRehearsalAttendanceAsync(_request, _ct).Returns(
            Page(pages.ElementAtOrDefault(0) ?? [], totalCount),
            pages.Skip(1).Select(names => Page(names, totalCount)).ToArray());

    private static Result<PagedList<RehearsalAttendanceReportDto>> Page(string[] names, int totalCount) =>
        Result<PagedList<RehearsalAttendanceReportDto>>.Success(new PagedList<RehearsalAttendanceReportDto>(
            names.Select(n => new RehearsalAttendanceReportDto { FullName = n, PresentCount = 2 }).ToList(), 1, 100, totalCount));

    [Fact]
    public async Task Export_GathersEveryPage_StoresTheFile_AndRecordsTheExport_Async()
    {
        ReturnsAttendancePages(2, ["Alice"], ["Bob"]);
        ReportExport? saved = null;
        await _exports.AddAsync(Arg.Do<ReportExport>(e => saved = e), _ct);

        var result = await _sut.ExportAsync(_adminId, _request, _ct);

        var file = result.Value!;
        Assert.StartsWith("RehearsalAttendance-", file.FileName);
        Assert.EndsWith(".csv", file.FileName);
        Assert.Equal([1, 2, 3], ((MemoryStream)file.Content).ToArray());
        Assert.Equal(3, file.Length);

        Assert.Equal(("fullName", "Họ tên"), _table!.Columns[0]);
        Assert.Equal(["Alice", "Bob"], _table.Rows.Select(r => r[0]));

        await _storage.Received(1).UploadAsync(Arg.Any<Stream>(), file.FileName, "report-exports", true, _ct);
        Assert.Equal((ReportType.RehearsalAttendance, _adminId, StoredId), (saved!.ReportType, saved.ExportedBy, saved.FileUrl));
        Assert.Contains("\"Format\":\"Csv\"", saved.Parameters);
        Assert.Contains(_request.EventId.ToString()!, saved.Parameters);
        await _exports.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Export_EmptyReport_ReturnsNoData_AndStoresNothing_Async()
    {
        ReturnsAttendancePages(0);

        var result = await _sut.ExportAsync(_adminId, _request, _ct);

        Assert.Equal(ErrorCodes.ReportNoData, result.Code);
        _writer.DidNotReceiveWithAnyArgs().Write(default!, default);
        await _storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task Export_StorageFails_ReturnsTheStorageCode_AndRecordsNothing_Async()
    {
        ReturnsAttendancePages(1, ["Alice"]);
        _storage.UploadAsync(default!, default!, default!, default, default)
            .ReturnsForAnyArgs(Result<FileUploadResponse>.Failure(ErrorCodes.ExternalStorageFailed));

        var result = await _sut.ExportAsync(_adminId, _request, _ct);

        Assert.Equal(ErrorCodes.ExternalStorageFailed, result.Code);
        await _exports.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task GetExports_ReadsTheStoredFilters_AndSignsTheDownloadUrl_Async()
    {
        var paging = new PagingRequest();
        var seasonId = Guid.NewGuid();
        var export = new ReportExport
        {
            Id = Guid.NewGuid(), ReportType = ReportType.SongUsage, FileUrl = StoredId, ExportedBy = _adminId,
            ExportedAt = DateTime.UtcNow, Exporter = new User { FullName = "Admin" },
            Parameters = $$"""{"Format":"Pdf","EventId":null,"LiturgicalSeasonId":"{{seasonId}}","FromDate":"2026-10-01","ToDate":null}""",
        };
        _exports.GetPageAsync(paging, _ct).Returns(new PagedList<ReportExport>([export], 1, 20, 1));
        _storage.GetSignedUrl(StoredId).Returns("https://files.test/signed");

        var row = Assert.Single((await _sut.GetExportsAsync(paging, _ct)).Value!.Items);

        Assert.Equal(
            (export.Id, ReportType.SongUsage, ReportFormat.Pdf, seasonId, new DateOnly(2026, 10, 1)),
            (row.Id, row.ReportType, row.Format, row.LiturgicalSeasonId!.Value, row.FromDate!.Value));
        Assert.Equal((_adminId, "Admin", "https://files.test/signed"), (row.ExportedBy, row.ExportedByName, row.DownloadUrl));
        Assert.Null(row.EventId);
    }
}
