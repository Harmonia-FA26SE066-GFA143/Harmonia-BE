namespace Harmonia.Application.DTOs;

/// <summary>The voice or clip attached to a declared skill. <see cref="Url"/> is short-lived and signed after the access check.</summary>
public class MemberSkillEvidenceDto
{
    /// <summary>"Voice" or "Clip".</summary>
    public string Type { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; }

    public string Url { get; set; } = string.Empty;
}
