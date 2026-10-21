using System.Text.Json;
using System.Text.Json.Serialization;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class ReportExportService(
    IReportService reportService,
    IReportFileWriter reportFileWriter,
    IFileStorageService fileStorageService,
    IReportExportRepository reportExportRepository,
    ICurrentUserService currentUserService) : IReportExportService
{
    private const int ExportPageSize = 100;

    private static readonly ReportType[] ChoirDirectorTypes =
        [ReportType.RehearsalAttendance, ReportType.Participation, ReportType.AssignmentCompletion];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>What <see cref="ReportExport.Parameters"/> holds; the entity has no column for the format.</summary>
    private sealed record Parameters(
        ReportFormat Format, Guid? EventId, Guid? LiturgicalSeasonId, DateOnly? FromDate, DateOnly? ToDate,
        Guid? ChoirId = null);

    public async Task<Result<FileContent>> ExportAsync(
        Guid userId, ExportReportRequest request, CancellationToken cancellationToken)
    {
        // The controller lets the Choir Director in for the per-member reports only; the type is not known to the attribute.
        if (currentUserService.RoleName == RoleNames.ChoirDirector
            && !(request.Type is { } type && ChoirDirectorTypes.Contains(type)))
            return Result<FileContent>.Failure(ErrorCodes.ReportTypeNotSupported);

        var table = request.Type switch
        {
            ReportType.UserActivity => ReportExportColumns.UserActivity(
                await AllRowsAsync(reportService.GetUserActivityAsync, request, cancellationToken)),
            ReportType.RehearsalAttendance => ReportExportColumns.RehearsalAttendance(
                await AllRowsAsync(reportService.GetRehearsalAttendanceAsync, request, cancellationToken)),
            ReportType.Participation => ReportExportColumns.Participation(
                await AllRowsAsync(reportService.GetParticipationAsync, request, cancellationToken)),
            ReportType.AssignmentCompletion => ReportExportColumns.AssignmentCompletion(
                await AllRowsAsync(reportService.GetAssignmentCompletionAsync, request, cancellationToken)),
            ReportType.SongUsage => ReportExportColumns.SongUsage(
                await AllRowsAsync(reportService.GetSongUsageAsync, request, cancellationToken)),
            ReportType.ServiceHistory => ReportExportColumns.ServiceHistory(
                await AllRowsAsync(reportService.GetServiceHistoryAsync, request, cancellationToken)),
            _ => null,
        };

        if (table is null) return Result<FileContent>.Failure(ErrorCodes.ReportTypeNotSupported);
        if (table.Rows.Count == 0) return Result<FileContent>.Failure(ErrorCodes.ReportNoData);

        var bytes = reportFileWriter.Write(table, request.Format);
        var exportedAt = DateTime.UtcNow;
        var fileName =
            $"{request.Type}-{VietnamTime.FromUtc(exportedAt):yyyyMMdd-HHmmss}.{request.Format.ToString().ToLowerInvariant()}";

        // Stored before it is handed over, so every file a user received is also in the history.
        using var upload = new MemoryStream(bytes);
        var stored = await fileStorageService.UploadAsync(
            upload, fileName, "report-exports", isPrivate: true, cancellationToken);
        if (!stored.IsSuccess) return Result<FileContent>.Failure(stored.Code!);

        await reportExportRepository.AddAsync(
            new ReportExport
            {
                Id = Guid.NewGuid(),
                ReportType = request.Type!.Value,
                Parameters = JsonSerializer.Serialize(
                    new Parameters(
                        request.Format, request.EventId, request.LiturgicalSeasonId, request.FromDate, request.ToDate,
                        request.ChoirId),
                    JsonOptions),
                // Private files have no usable URL, so the column keeps the id a signed URL is built from.
                FileUrl = stored.Value!.PublicId,
                ExportedBy = userId,
                ExportedAt = exportedAt,
            },
            cancellationToken);
        await reportExportRepository.SaveChangesAsync(cancellationToken);

        return Result<FileContent>.Success(new FileContent(new MemoryStream(bytes), fileName, bytes.Length));
    }

    public async Task<Result<PagedList<ReportExportDto>>> GetExportsAsync(
        PagingRequest request, CancellationToken cancellationToken)
    {
        var page = await reportExportRepository.GetPageAsync(request, cancellationToken);

        var items = page.Items.Select(export =>
        {
            var parameters = JsonSerializer.Deserialize<Parameters>(export.Parameters, JsonOptions)!;
            return new ReportExportDto
            {
                Id = export.Id,
                ReportType = export.ReportType,
                Format = parameters.Format,
                EventId = parameters.EventId,
                LiturgicalSeasonId = parameters.LiturgicalSeasonId,
                FromDate = parameters.FromDate,
                ToDate = parameters.ToDate,
                ExportedBy = export.ExportedBy,
                ExportedByName = export.Exporter.FullName,
                ExportedAt = export.ExportedAt,
                DownloadUrl = fileStorageService.GetSignedUrl(export.FileUrl),
            };
        }).ToList();

        return Result<PagedList<ReportExportDto>>.Success(
            new PagedList<ReportExportDto>(items, page.PageNumber, page.PageSize, page.TotalCount));
    }

    /// <summary>Walks the report page by page; the report endpoints cap a page at 100 rows.</summary>
    private static async Task<List<T>> AllRowsAsync<T>(
        Func<SearchReportsRequest, CancellationToken, Task<Result<PagedList<T>>>> getPage,
        SearchReportsRequest request,
        CancellationToken cancellationToken)
    {
        var rows = new List<T>();
        request.PageSize = ExportPageSize;

        for (request.PageNumber = 1; ; request.PageNumber++)
        {
            var page = (await getPage(request, cancellationToken)).Value!;
            rows.AddRange(page.Items);
            if (page.Items.Count == 0 || rows.Count >= page.TotalCount) return rows;
        }
    }
}
