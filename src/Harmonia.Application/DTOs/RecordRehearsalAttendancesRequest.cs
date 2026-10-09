namespace Harmonia.Application.DTOs;

/// <summary>
/// Body of PUT api/rehearsals/{id}/attendances: each listed member is recorded or corrected.
/// Members left out keep whatever they had.
/// </summary>
public class RecordRehearsalAttendancesRequest
{
    public List<RecordRehearsalAttendanceRequest> Items { get; set; } = [];
}
