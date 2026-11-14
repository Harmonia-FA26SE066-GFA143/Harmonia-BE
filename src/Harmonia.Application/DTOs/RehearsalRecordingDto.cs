namespace Harmonia.Application.DTOs;

public class RehearsalRecordingDto
{
    public Guid Id { get; set; }

    public Guid RehearsalId { get; set; }

    public Guid? SongId { get; set; }

    public string? SongTitle { get; set; }

    public string? Title { get; set; }

    public string FileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public int? DurationSeconds { get; set; }

    /// <summary>Short-lived signed URL; read the list again when it expires.</summary>
    public string AudioUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
