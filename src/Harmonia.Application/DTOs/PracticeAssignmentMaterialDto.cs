using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>A material attached to a practice assignment; open it through api/music-materials by its id.</summary>
public class PracticeAssignmentMaterialDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public MaterialType MaterialType { get; set; }
}
