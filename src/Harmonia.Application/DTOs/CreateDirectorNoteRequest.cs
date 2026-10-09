namespace Harmonia.Application.DTOs;

/// <summary>
/// Body of POST api/director-notes. The note is about a day, an event, or both; each recipient must be an
/// active Choir Director and gets their own copy. The sender is the caller, never taken from the body.
/// </summary>
public class CreateDirectorNoteRequest
{
    public DateOnly? NoteDate { get; set; }

    public Guid? EventId { get; set; }

    public List<Guid> ToUserIds { get; set; } = [];

    public string Content { get; set; } = string.Empty;
}
