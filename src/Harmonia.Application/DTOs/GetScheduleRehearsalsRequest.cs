using System;

namespace Harmonia.Application.DTOs;

/// <summary>
/// Query string of GET api/schedule/rehearsals. Without dates the upcoming rehearsals are returned; with both, the
/// rehearsals starting in that range (inclusive), past ones included.
/// </summary>
public class GetScheduleRehearsalsRequest
{
    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }
}
