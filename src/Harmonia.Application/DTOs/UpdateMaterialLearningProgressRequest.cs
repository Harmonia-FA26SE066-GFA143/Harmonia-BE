using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>Body of PUT api/music-materials/{id}/learning-progress (UC-08 / FE-09).</summary>
public class UpdateMaterialLearningProgressRequest
{
    /// <summary>Learned or NeedsPractice; NotStarted only means no row exists yet and cannot be set.</summary>
    public LearningStatus Status { get; set; }
}
