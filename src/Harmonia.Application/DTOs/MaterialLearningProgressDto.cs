using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class MaterialLearningProgressDto
{
    public Guid MaterialId { get; set; }

    public LearningStatus Status { get; set; }

    public DateTime UpdatedAt { get; set; }
}
