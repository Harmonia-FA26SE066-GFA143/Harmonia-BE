using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class RecordDirectorTrainingResultRequest
{
    /// <summary>Passed or Failed; InProgress is rejected.</summary>
    public DirectorTrainingResult Result { get; set; }

    public string? Note { get; set; }
}
