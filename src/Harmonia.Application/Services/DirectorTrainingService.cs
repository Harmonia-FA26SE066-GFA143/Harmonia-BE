using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class DirectorTrainingService(
    IGenericRepository<DirectorTraining> trainingRepository,
    IDirectorNominationRepository nominationRepository,
    IFileStorageService fileStorageService,
    INotificationService notificationService,
    ICurrentUserService currentUser,
    IMapper mapper) : IDirectorTrainingService
{
    private const string CertificateFolder = "director-training-certificates";
    private const long MaxCertificateSizeBytes = 20 * 1024 * 1024;
    private static readonly HashSet<string> CertificateExtensions = [".pdf", ".jpg", ".png"];

    public async Task<Result<List<DirectorTrainingDto>>> GetByNominationAsync(
        Guid nominationId, CancellationToken cancellationToken)
    {
        // Same visibility as the nomination itself: a director their own, a member theirs once approved.
        var (nominatedById, memberUserId) = currentUser.RoleName switch
        {
            RoleNames.ChoirDirector => (currentUser.UserId, (Guid?)null),
            RoleNames.ChoirMember => (null, currentUser.UserId),
            _ => ((Guid?)null, (Guid?)null),
        };
        if (await nominationRepository.GetWithDetailsAsync(
                nominationId, nominatedById, memberUserId, cancellationToken) is null)
        {
            return Result<List<DirectorTrainingDto>>.Failure(ErrorCodes.DirectorNominationNotFound);
        }

        var trainings = await trainingRepository.ListAsync(x => x.NominationId == nominationId, cancellationToken);
        return Result<List<DirectorTrainingDto>>.Success(
            trainings.OrderBy(x => x.StartDate).ThenBy(x => x.CreatedAt).Select(ToDto).ToList());
    }

    public async Task<Result<DirectorTrainingDto>> CreateAsync(
        Guid nominationId, CreateDirectorTrainingRequest request, CancellationToken cancellationToken)
    {
        var nomination = await nominationRepository.GetByIdAsync(nominationId, cancellationToken);
        if (nomination is null)
        {
            return Result<DirectorTrainingDto>.Failure(ErrorCodes.DirectorNominationNotFound);
        }

        if (nomination.Status == DirectorNominationStatus.InTraining)
        {
            return Result<DirectorTrainingDto>.Failure(ErrorCodes.DirectorTrainingInProgress);
        }

        // Trained is allowed too: a member who passed a basic course can be sent on to an advanced one.
        if (nomination.Status is not (DirectorNominationStatus.Approved or DirectorNominationStatus.Trained))
        {
            return Result<DirectorTrainingDto>.Failure(ErrorCodes.DirectorNominationInvalidStatus);
        }

        var training = new DirectorTraining
        {
            Id = Guid.NewGuid(),
            NominationId = nominationId,
            CourseName = request.CourseName.Trim(),
            Organizer = TrimToNull(request.Organizer),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Result = DirectorTrainingResult.InProgress,
        };
        await trainingRepository.AddAsync(training, cancellationToken);
        nomination.Status = DirectorNominationStatus.InTraining;
        await trainingRepository.SaveChangesAsync(cancellationToken);

        await NotifyAsync(nominationId, "Cử đi học lớp ca trưởng",
            $"Cha xứ đã cử đi học khoá \"{training.CourseName}\", bắt đầu ngày {training.StartDate:dd/MM/yyyy}.",
            cancellationToken);

        return Result<DirectorTrainingDto>.Success(ToDto(training));
    }

    public async Task<Result<DirectorTrainingDto>> RecordResultAsync(
        Guid nominationId, Guid trainingId, RecordDirectorTrainingResultRequest request,
        CancellationToken cancellationToken)
    {
        var training = await GetTrainingAsync(nominationId, trainingId, cancellationToken);
        if (training is null)
        {
            return Result<DirectorTrainingDto>.Failure(ErrorCodes.DirectorTrainingNotFound);
        }

        if (training.Result != DirectorTrainingResult.InProgress)
        {
            return Result<DirectorTrainingDto>.Failure(ErrorCodes.DirectorTrainingAlreadyRecorded);
        }

        var nomination = (await nominationRepository.GetByIdAsync(nominationId, cancellationToken))!;
        var qualifiedBefore = await HasQualifiedAsync(nomination, trainingId, cancellationToken);

        training.Result = request.Result;
        training.ResultNote = TrimToNull(request.Note);
        training.ResultRecordedAt = DateTime.UtcNow;
        nomination.Status = request.Result == DirectorTrainingResult.Passed || qualifiedBefore
            ? DirectorNominationStatus.Trained
            : DirectorNominationStatus.Approved;
        await trainingRepository.SaveChangesAsync(cancellationToken);

        var outcome = request.Result == DirectorTrainingResult.Passed ? "đạt" : "không đạt";
        await NotifyAsync(nominationId, "Kết quả lớp ca trưởng",
            $"Khoá \"{training.CourseName}\": {outcome}.", cancellationToken);

        return Result<DirectorTrainingDto>.Success(ToDto(training));
    }

    public async Task<Result<DirectorTrainingDto>> UploadCertificateAsync(
        Guid nominationId, Guid trainingId, FileContent? file, CancellationToken cancellationToken)
    {
        var training = await GetTrainingAsync(nominationId, trainingId, cancellationToken);
        if (training is null)
        {
            return Result<DirectorTrainingDto>.Failure(ErrorCodes.DirectorTrainingNotFound);
        }

        if (CheckCertificate(file) is { } fileError)
        {
            return Result<DirectorTrainingDto>.Failure(fileError);
        }

        var upload = await fileStorageService.UploadAsync(
            file!.Content, file.FileName, CertificateFolder, isPrivate: true, cancellationToken);
        if (!upload.IsSuccess)
        {
            return Result<DirectorTrainingDto>.Failure(upload.Code!);
        }

        var newPublicId = upload.Value!.PublicId;
        var previousPublicId = training.CertificatePublicId;
        training.CertificatePublicId = newPublicId;
        training.CertificateFileName = Path.GetFileName(file.FileName);
        training.CertificateSizeBytes = file.Length;

        try
        {
            await trainingRepository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // No row points at the file, so remove it rather than leave an orphan in storage.
            await fileStorageService.DeleteAsync(newPublicId, isPrivate: true, CancellationToken.None);
            throw;
        }

        // The row already points at the new file; an old file the provider failed to delete is only an orphan.
        if (previousPublicId is not null)
        {
            await fileStorageService.DeleteAsync(previousPublicId, isPrivate: true, CancellationToken.None);
        }

        return Result<DirectorTrainingDto>.Success(ToDto(training));
    }

    public async Task<Result> DeleteAsync(Guid nominationId, Guid trainingId, CancellationToken cancellationToken)
    {
        var training = await GetTrainingAsync(nominationId, trainingId, cancellationToken);
        if (training is null)
        {
            return Result.Failure(ErrorCodes.DirectorTrainingNotFound);
        }

        // A recorded result is history; only a course still running can have been entered by mistake.
        if (training.Result != DirectorTrainingResult.InProgress)
        {
            return Result.Failure(ErrorCodes.DirectorTrainingAlreadyRecorded);
        }

        var nomination = (await nominationRepository.GetByIdAsync(nominationId, cancellationToken))!;
        nomination.Status = await HasQualifiedAsync(nomination, trainingId, cancellationToken)
            ? DirectorNominationStatus.Trained
            : DirectorNominationStatus.Approved;
        trainingRepository.Remove(training);
        await trainingRepository.SaveChangesAsync(cancellationToken);

        if (training.CertificatePublicId is not null)
        {
            await fileStorageService.DeleteAsync(training.CertificatePublicId, isPrivate: true, CancellationToken.None);
        }

        return Result.Success();
    }

    private async Task<DirectorTraining?> GetTrainingAsync(
        Guid nominationId, Guid trainingId, CancellationToken cancellationToken)
    {
        var training = await trainingRepository.GetByIdAsync(trainingId, cancellationToken);
        return training?.NominationId == nominationId ? training : null;
    }

    /// <summary>Whether the member was already cleared before this course: exempted, or passed another course.</summary>
    private async Task<bool> HasQualifiedAsync(
        DirectorNomination nomination, Guid exceptTrainingId, CancellationToken cancellationToken) =>
        nomination.TrainingWaiverReason is not null
        || (await trainingRepository.ListAsync(
                x => x.NominationId == nomination.Id && x.Id != exceptTrainingId
                    && x.Result == DirectorTrainingResult.Passed,
                cancellationToken))
            .Count > 0;

    private DirectorTrainingDto ToDto(DirectorTraining training)
    {
        var dto = mapper.Map<DirectorTrainingDto>(training);
        dto.CertificateUrl = training.CertificatePublicId is null
            ? null
            : fileStorageService.GetSignedUrl(training.CertificatePublicId);
        return dto;
    }

    private static string? CheckCertificate(FileContent? file)
    {
        if (file is null || file.Length == 0)
        {
            return ErrorCodes.DirectorTrainingCertificateRequired;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!CertificateExtensions.Contains(extension))
        {
            return ErrorCodes.DirectorTrainingCertificateTypeNotAllowed;
        }

        return file.Length > MaxCertificateSizeBytes ? ErrorCodes.DirectorTrainingCertificateTooLarge : null;
    }

    /// <summary>Tells the member and the nominator; the Parish Priest acting is never told about their own action.</summary>
    private async Task NotifyAsync(Guid nominationId, string title, string content, CancellationToken cancellationToken)
    {
        var nomination = await nominationRepository.GetWithDetailsAsync(nominationId, null, null, cancellationToken);
        var recipients = new[] { nomination!.Member.UserId, nomination.NominatedById }
            .Where(x => x != currentUser.UserId)
            .Distinct()
            .ToList();
        if (recipients.Count == 0)
        {
            return;
        }

        await notificationService.SendAsync(
            new SendNotificationRequest(NotificationType.DirectorNomination, title, content, recipients,
                nameof(DirectorNomination), nominationId),
            cancellationToken);
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
