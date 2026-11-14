using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Services;

public class RehearsalRecordingService(
    IRehearsalRecordingRepository recordingRepository,
    IRehearsalRepository rehearsalRepository,
    IMemberProfileRepository memberProfileRepository,
    IGenericRepository<Song> songRepository,
    IFileStorageService fileStorageService,
    IChoirScopeService choirScope,
    IMapper mapper) : IRehearsalRecordingService
{
    public const long MaxAudioSizeBytes = 20 * 1024 * 1024;

    // Browsers record .webm / .ogg (Chrome, Firefox) or .m4a (Safari); uploaded files may also be .mp3 or .wav.
    private static readonly string[] AudioExtensions = [".webm", ".ogg", ".m4a", ".mp3", ".wav"];

    private const string Folder = "rehearsal-recordings";

    public async Task<Result<List<RehearsalRecordingDto>>> GetAsync(
        Guid userId, bool isDirector, Guid rehearsalId, CancellationToken cancellationToken)
    {
        if (!await CanViewAsync(userId, isDirector, rehearsalId, cancellationToken))
            return Result<List<RehearsalRecordingDto>>.Failure(ErrorCodes.RehearsalNotFound);

        var recordings = await recordingRepository.GetByRehearsalAsync(rehearsalId, cancellationToken);
        return Result<List<RehearsalRecordingDto>>.Success(recordings.Select(ToDto).ToList());
    }

    public async Task<Result<RehearsalRecordingDto>> CreateAsync(
        Guid rehearsalId, CreateRehearsalRecordingRequest request, FileContent? file, CancellationToken cancellationToken)
    {
        var rehearsal = await rehearsalRepository.GetByIdAsync(rehearsalId, cancellationToken);
        if (rehearsal is null || !await choirScope.CanManageChoirAsync(rehearsal.ChoirId, cancellationToken))
            return Result<RehearsalRecordingDto>.Failure(ErrorCodes.RehearsalNotFound);

        Song? song = null;
        if (request.SongId is { } songId)
        {
            song = await songRepository.GetByIdAsync(songId, cancellationToken);
            if (song is null) return Result<RehearsalRecordingDto>.Failure(ErrorCodes.SongNotFound);
        }

        if (CheckAudio(file) is { } fileError) return Result<RehearsalRecordingDto>.Failure(fileError);

        var upload = await fileStorageService.UploadAsync(file!.Content, file.FileName, Folder, isPrivate: true, cancellationToken);
        if (!upload.IsSuccess) return Result<RehearsalRecordingDto>.Failure(upload.Code!);

        var recording = new RehearsalRecording
        {
            Id = Guid.NewGuid(),
            RehearsalId = rehearsal.Id,
            SongId = song?.Id,
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            AudioPublicId = upload.Value!.PublicId,
            FileName = Path.GetFileName(file.FileName),
            FileSizeBytes = file.Length,
            DurationSeconds = request.DurationSeconds,
        };

        try
        {
            await recordingRepository.AddAsync(recording, cancellationToken);
            await recordingRepository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // No row points at the file, so remove it rather than leave an orphan in storage.
            await fileStorageService.DeleteAsync(recording.AudioPublicId, isPrivate: true, CancellationToken.None);
            throw;
        }

        recording.Song = song;
        return Result<RehearsalRecordingDto>.Success(ToDto(recording));
    }

    public async Task<Result> DeleteAsync(Guid rehearsalId, Guid recordingId, CancellationToken cancellationToken)
    {
        var rehearsal = await rehearsalRepository.GetByIdAsync(rehearsalId, cancellationToken);
        if (rehearsal is null || !await choirScope.CanManageChoirAsync(rehearsal.ChoirId, cancellationToken))
            return Result.Failure(ErrorCodes.RehearsalNotFound);

        var recording = await recordingRepository.GetByIdAsync(recordingId, cancellationToken);
        if (recording is null || recording.RehearsalId != rehearsalId)
            return Result.Failure(ErrorCodes.RehearsalRecordingNotFound);

        recordingRepository.Remove(recording);
        await recordingRepository.SaveChangesAsync(cancellationToken);

        // The row is gone either way; a file the provider failed to delete is only an orphan in storage.
        await fileStorageService.DeleteAsync(recording.AudioPublicId, isPrivate: true, CancellationToken.None);
        return Result.Success();
    }

    private async Task<bool> CanViewAsync(Guid userId, bool isDirector, Guid rehearsalId, CancellationToken cancellationToken)
    {
        if (isDirector)
        {
            var rehearsal = await rehearsalRepository.GetByIdAsync(rehearsalId, cancellationToken);
            return rehearsal is not null && await choirScope.CanManageChoirAsync(rehearsal.ChoirId, cancellationToken);
        }

        var member = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return false;

        var choirIds = await choirScope.GetMemberChoirIdsAsync(member.Id, cancellationToken);
        return await rehearsalRepository.GetVisibleForMemberAsync(rehearsalId, member.Id, choirIds, cancellationToken) is not null;
    }

    /// <summary>Extension and size come from the server-side upload; the client's Content-Type is never trusted.</summary>
    private static string? CheckAudio(FileContent? file)
    {
        if (file is null || file.Length == 0) return ErrorCodes.RehearsalRecordingRequired;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AudioExtensions.Contains(extension)) return ErrorCodes.RehearsalRecordingTypeNotAllowed;

        return file.Length > MaxAudioSizeBytes ? ErrorCodes.RehearsalRecordingTooLarge : null;
    }

    private RehearsalRecordingDto ToDto(RehearsalRecording recording)
    {
        var dto = mapper.Map<RehearsalRecordingDto>(recording);
        dto.AudioUrl = fileStorageService.GetSignedUrl(recording.AudioPublicId);
        return dto;
    }
}
