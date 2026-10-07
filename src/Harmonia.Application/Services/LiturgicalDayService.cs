using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Services;

public class LiturgicalDayService(
    ILiturgicalDayRepository liturgicalDayRepository,
    ICatholicCalendarParser calendarParser,
    IMapper mapper) : ILiturgicalDayService
{
    private const string AllowedExtension = ".ics";

    private const long MaxFileSizeBytes = 2 * 1024 * 1024;

    public async Task<Result<LiturgicalDayDto>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var liturgicalDay = await liturgicalDayRepository.GetByDateAsync(date, cancellationToken);

        return liturgicalDay is null
            ? Result<LiturgicalDayDto>.Failure(ErrorCodes.CalendarDayNotFound)
            : Result<LiturgicalDayDto>.Success(mapper.Map<LiturgicalDayDto>(liturgicalDay));
    }

    public async Task<Result<int>> ImportAsync(
        Stream content,
        string fileName,
        long length,
        CancellationToken cancellationToken)
    {
        // Whitelist the extension and cap the size on the server; the client's Content-Type is never trusted.
        if (length <= 0)
        {
            return Result<int>.Failure(ErrorCodes.CalendarFileRequired);
        }

        if (!string.Equals(Path.GetExtension(fileName), AllowedExtension, StringComparison.OrdinalIgnoreCase))
        {
            return Result<int>.Failure(ErrorCodes.CalendarFileTypeNotAllowed);
        }

        if (length > MaxFileSizeBytes)
        {
            return Result<int>.Failure(ErrorCodes.CalendarFileTooLarge);
        }

        var parsed = await calendarParser.ParseAsync(content, cancellationToken);
        if (!parsed.IsSuccess)
        {
            return Result<int>.Failure(parsed.Code!);
        }

        var fetchedAt = DateTime.Now;
        var newDays = new List<LiturgicalDay>();

        // A yearly feed can spill into the neighbouring year, so check the cache year by year.
        foreach (var yearGroup in parsed.Value!.GroupBy(x => x.Date.Year))
        {
            var existingDates = await liturgicalDayRepository.GetCachedDatesInYearAsync(yearGroup.Key, cancellationToken);

            newDays.AddRange(yearGroup
                .Where(x => !existingDates.Contains(x.Date))
                .Select(x => new LiturgicalDay
                {
                    Id = Guid.NewGuid(),
                    Date = x.Date,
                    CelebrationName = x.CelebrationName,
                    Rank = x.Rank,
                    SeasonName = x.SeasonName,
                    FetchedAt = fetchedAt,
                }));
        }

        if (newDays.Count > 0)
        {
            await liturgicalDayRepository.AddRangeAsync(newDays, cancellationToken);
            await liturgicalDayRepository.SaveChangesAsync(cancellationToken);
        }

        return Result<int>.Success(newDays.Count);
    }
}