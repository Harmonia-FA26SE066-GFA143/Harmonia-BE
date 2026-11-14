namespace Harmonia.Application.DTOs;

/// <summary>One row of GET api/director-notes/conversations: the other party, the latest note and how many are unread.</summary>
public class DirectorNoteConversationDto
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public DirectorNoteDto LastNote { get; set; } = null!;

    /// <summary>Notes the other party sent the caller that the caller has not read.</summary>
    public int UnreadCount { get; set; }
}
