using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>A material as the calling member sees it in GET api/music-materials/mine, with their own learning progress.</summary>
public class MusicMaterialDetailDto : MusicMaterialDto
{
    /// <summary>NotStarted when the member has never marked the material.</summary>
    public LearningStatus LearningStatus { get; set; }

    public DateTime? LearningUpdatedAt { get; set; }
}
