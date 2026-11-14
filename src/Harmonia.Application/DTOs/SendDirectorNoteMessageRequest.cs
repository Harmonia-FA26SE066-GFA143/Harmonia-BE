namespace Harmonia.Application.DTOs;

/// <summary>
/// Body of POST api/director-notes/messages: a chat message between the Parish Priest and a Choir Director,
/// in either direction. The sender is the caller, never taken from the body.
/// </summary>
public class SendDirectorNoteMessageRequest
{
    public Guid ToUserId { get; set; }

    public string Content { get; set; } = string.Empty;
}
