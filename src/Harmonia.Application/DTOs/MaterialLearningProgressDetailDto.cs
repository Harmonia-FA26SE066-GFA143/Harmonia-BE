using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>One member expected to learn a material, as the Choir Director sees it.</summary>
public class MaterialLearningProgressDetailDto
{
    public Guid MemberId { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>NotStarted when the member has never marked the material.</summary>
    public LearningStatus Status { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
