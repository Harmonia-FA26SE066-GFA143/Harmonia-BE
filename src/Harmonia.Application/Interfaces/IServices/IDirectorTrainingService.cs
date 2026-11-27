using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Courses a nominated member is sent to (UC-46, step 9 of docs/harmonia-director-nomination-process.md). Only the
/// Parish Priest writes; whoever may see the nomination reads its courses.
/// </summary>
public interface IDirectorTrainingService
{
    Task<Result<List<DirectorTrainingDto>>> GetByNominationAsync(Guid nominationId, CancellationToken cancellationToken);

    /// <summary>Approved or Trained → InTraining; one course in progress at a time.</summary>
    Task<Result<DirectorTrainingDto>> CreateAsync(
        Guid nominationId, CreateDirectorTrainingRequest request, CancellationToken cancellationToken);

    /// <summary>Passed → nomination Trained. Failed → back to Trained if the member already qualified, else Approved.</summary>
    Task<Result<DirectorTrainingDto>> RecordResultAsync(
        Guid nominationId, Guid trainingId, RecordDirectorTrainingResultRequest request, CancellationToken cancellationToken);

    /// <summary>.pdf, .jpg, .png up to 20 MB, stored private; a new upload replaces the previous file.</summary>
    Task<Result<DirectorTrainingDto>> UploadCertificateAsync(
        Guid nominationId, Guid trainingId, FileContent? file, CancellationToken cancellationToken);

    /// <summary>Removes a course entered by mistake while still in progress; the nomination returns to where it was.</summary>
    Task<Result> DeleteAsync(Guid nominationId, Guid trainingId, CancellationToken cancellationToken);
}
