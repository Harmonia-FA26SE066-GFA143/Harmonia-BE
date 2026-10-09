namespace Harmonia.Application.DTOs;

public class DirectorNoteDto
{
    public Guid Id { get; set; }

    public DateOnly? NoteDate { get; set; }

    public Guid? EventId { get; set; }

    public string? EventTitle { get; set; }

    public Guid FromUserId { get; set; }

    public string FromUserName { get; set; } = string.Empty;

    public Guid ToUserId { get; set; }

    public string ToUserName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }
}
