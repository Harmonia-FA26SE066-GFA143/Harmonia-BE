namespace Harmonia.Application.DTOs;

/// <summary>Body of PUT api/service-rosters/assignments/{id}/note; null or blank clears the note.</summary>
public class UpdateRosterAssignmentNoteRequest
{
    public string? Note { get; set; }
}
