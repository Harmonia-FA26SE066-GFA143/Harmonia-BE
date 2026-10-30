using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class MusicMaterialService(
    IMusicMaterialRepository musicMaterialRepository,
    ISongRepository songRepository,
    IGenericRepository<Skill> skillRepository,
    IMemberProfileRepository memberProfileRepository,
    IMaterialLearningProgressRepository learningProgressRepository,
    IFileStorageService fileStorageService,
    IMidiRenderer midiRenderer,
    IMapper mapper) : IMusicMaterialService
{
    private const string PlaybackFolder = "music-materials/playback";

    private static readonly string[] MidiExtensions = [".mid", ".midi"];

    public const long MaxFileSizeBytes = 20 * 1024 * 1024;

    // Matches MusicMaterialConfiguration.FileName.
    private const int MaxFileNameLength = 200;

    private static readonly string[] DocumentExtensions = [".pdf", ".png", ".jpg"];

    private static readonly string[] AudioExtensions = [".mp3", ".m4a", ".wav", ".mid", ".midi"];

    private static readonly string[] LyricsExtensions = [".docx"];

    public async Task<Result<MusicMaterialDto>> UploadAsync(
        UploadMusicMaterialRequest request, FileContent? file, CancellationToken cancellationToken)
    {
        var song = await songRepository.GetByIdAsync(request.SongId, cancellationToken);
        if (song is not { IsActive: true }) return Result<MusicMaterialDto>.Failure(ErrorCodes.SongNotFound);

        var (targetSkill, skillError) = await GetTargetSkillAsync(request.TargetSkillId, cancellationToken);
        if (skillError is not null) return Result<MusicMaterialDto>.Failure(skillError);

        if (request.MaterialType == MaterialType.Lyrics)
        {
            var hasText = !string.IsNullOrWhiteSpace(request.LyricsText);
            if (hasText == file is not null) return Result<MusicMaterialDto>.Failure(ErrorCodes.MaterialLyricsSourceInvalid);
            if (hasText) return await AddLyricsTextAsync(request, targetSkill, cancellationToken);
        }

        if (CheckFile(request.MaterialType, file) is { } fileError)
            return Result<MusicMaterialDto>.Failure(fileError);

        var playback = await StorePlaybackAsync(file!, request.Instrument, cancellationToken);
        if (!playback.IsSuccess) return Result<MusicMaterialDto>.Failure(playback.Code!);

        var upload = await fileStorageService.UploadAsync(
            file!.Content, file.FileName, FolderFor(request.MaterialType), isPrivate: true, cancellationToken);
        if (!upload.IsSuccess)
        {
            await DeletePlaybackAsync(playback.Value?.AudioPublicId, playback.Value?.NotesPublicId);
            return Result<MusicMaterialDto>.Failure(upload.Code!);
        }

        var publicId = upload.Value!.PublicId;
        var fileName = Path.GetFileName(file.FileName);
        var material = mapper.Map<MusicMaterial>(request);
        material.Id = Guid.NewGuid();
        material.FilePublicId = publicId;
        // Keep the tail so the extension survives truncation.
        material.FileName = fileName.Length > MaxFileNameLength ? fileName[^MaxFileNameLength..] : fileName;
        material.FileSizeBytes = file.Length;
        SetPlayback(material, playback.Value);

        try
        {
            await musicMaterialRepository.AddAsync(material, cancellationToken);
            await musicMaterialRepository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // No row points at the file, so remove it rather than leave an orphan in storage.
            // Best effort: a failed cleanup is logged by the storage service and must not hide the save error.
            // ponytail: an orphan survives if cleanup fails too; add a storage sweep job if that ever matters.
            await fileStorageService.DeleteAsync(publicId, isPrivate: true, CancellationToken.None);
            await DeletePlaybackAsync(material.PlaybackAudioPublicId, material.PlaybackNotesPublicId);
            throw;
        }

        material.TargetSkill = targetSkill;
        return Result<MusicMaterialDto>.Success(ToDto<MusicMaterialDto>(material));
    }

    public async Task<Result<PagedList<MusicMaterialDto>>> GetBySongAsync(
        Guid songId, PagingRequest paging, CancellationToken cancellationToken)
    {
        var song = await songRepository.GetByIdAsync(songId, cancellationToken);
        if (song is not { IsActive: true }) return Result<PagedList<MusicMaterialDto>>.Failure(ErrorCodes.SongNotFound);

        var page = await musicMaterialRepository.GetActiveBySongAsync(songId, paging, cancellationToken);
        return Result<PagedList<MusicMaterialDto>>.Success(ToDtoPage<MusicMaterialDto>(page));
    }

    public async Task<Result<PagedList<MusicMaterialDetailDto>>> GetMineAsync(
        Guid userId, SearchMusicMaterialsRequest request, CancellationToken cancellationToken)
    {
        var member = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return Result<PagedList<MusicMaterialDetailDto>>.Failure(ErrorCodes.MemberNotFound);

        if (request.SongId is { } songId)
        {
            var song = await songRepository.GetByIdAsync(songId, cancellationToken);
            if (song is not { IsActive: true }) return Result<PagedList<MusicMaterialDetailDto>>.Failure(ErrorCodes.SongNotFound);
        }

        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();

        // ponytail: filters by approved skills only; add the finalized-roster song filter once ServiceRoster ships.
        var page = await musicMaterialRepository.GetActiveForMemberAsync(member.Id, keyword, request, cancellationToken);
        return Result<PagedList<MusicMaterialDetailDto>>.Success(ToDtoPage<MusicMaterialDetailDto>(page));
    }

    public async Task<Result<PagedList<MaterialLearningProgressDetailDto>>> GetLearningProgressAsync(
        Guid directorUserId, Guid materialId, SearchMaterialLearningProgressRequest request,
        CancellationToken cancellationToken)
    {
        var material = await musicMaterialRepository.GetByIdAsync(materialId, cancellationToken);
        if (material is not { IsActive: true })
            return Result<PagedList<MaterialLearningProgressDetailDto>>.Failure(ErrorCodes.MaterialNotFound);

        var page = await memberProfileRepository.GetLearnersOfMaterialAsync(
            material.Id, material.TargetSkillId, directorUserId, request, cancellationToken);
        return Result<PagedList<MaterialLearningProgressDetailDto>>.Success(new PagedList<MaterialLearningProgressDetailDto>(
            mapper.Map<List<MaterialLearningProgressDetailDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<MaterialLearningProgressDto>> UpdateLearningProgressAsync(
        Guid userId, Guid materialId, UpdateMaterialLearningProgressRequest request, CancellationToken cancellationToken)
    {
        var member = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return Result<MaterialLearningProgressDto>.Failure(ErrorCodes.MemberNotFound);

        // Same scope as GetMineAsync: a material the member cannot list is reported as missing, not forbidden.
        if (!await musicMaterialRepository.IsVisibleToMemberAsync(materialId, member.Id, cancellationToken))
            return Result<MaterialLearningProgressDto>.Failure(ErrorCodes.MaterialNotFound);

        var progress = await learningProgressRepository.UpsertAsync(member.Id, materialId, request.Status, cancellationToken);
        return Result<MaterialLearningProgressDto>.Success(mapper.Map<MaterialLearningProgressDto>(progress));
    }

    public async Task<Result<MusicMaterialDto>> UpdateAsync(
        Guid id, UpdateMusicMaterialRequest request, CancellationToken cancellationToken)
    {
        var material = await musicMaterialRepository.GetByIdAsync(id, cancellationToken);
        if (material is not { IsActive: true }) return Result<MusicMaterialDto>.Failure(ErrorCodes.MaterialNotFound);

        var (targetSkill, skillError) = await GetTargetSkillAsync(request.TargetSkillId, cancellationToken);
        if (skillError is not null) return Result<MusicMaterialDto>.Failure(skillError);

        // Text lyrics must keep their text; a material with a file takes no text.
        if ((material.FilePublicId is null) == string.IsNullOrWhiteSpace(request.LyricsText))
            return Result<MusicMaterialDto>.Failure(ErrorCodes.MaterialLyricsSourceInvalid);

        mapper.Map(request, material);
        await musicMaterialRepository.SaveChangesAsync(cancellationToken);

        material.TargetSkill = targetSkill;
        return Result<MusicMaterialDto>.Success(ToDto<MusicMaterialDto>(material));
    }

    public async Task<Result<MusicMaterialDto>> ReplaceFileAsync(
        Guid id, ReplaceMusicMaterialFileRequest request, FileContent? file, CancellationToken cancellationToken)
    {
        var material = await musicMaterialRepository.GetByIdAsync(id, cancellationToken);
        if (material is not { IsActive: true }) return Result<MusicMaterialDto>.Failure(ErrorCodes.MaterialNotFound);
        if (material.FilePublicId is null) return Result<MusicMaterialDto>.Failure(ErrorCodes.MaterialLyricsSourceInvalid);

        if (CheckFile(material.MaterialType, file) is { } fileError)
            return Result<MusicMaterialDto>.Failure(fileError);

        var playback = await StorePlaybackAsync(file!, request.Instrument, cancellationToken);
        if (!playback.IsSuccess) return Result<MusicMaterialDto>.Failure(playback.Code!);

        var upload = await fileStorageService.UploadAsync(
            file!.Content, file.FileName, FolderFor(material.MaterialType), isPrivate: true, cancellationToken);
        if (!upload.IsSuccess)
        {
            await DeletePlaybackAsync(playback.Value?.AudioPublicId, playback.Value?.NotesPublicId);
            return Result<MusicMaterialDto>.Failure(upload.Code!);
        }

        var oldPublicId = material.FilePublicId;
        var (oldAudioId, oldNotesId) = (material.PlaybackAudioPublicId, material.PlaybackNotesPublicId);
        var newPublicId = upload.Value!.PublicId;
        var fileName = Path.GetFileName(file.FileName);
        material.FilePublicId = newPublicId;
        material.FileName = fileName.Length > MaxFileNameLength ? fileName[^MaxFileNameLength..] : fileName;
        material.FileSizeBytes = file.Length;
        // A non-MIDI replacement clears the playback files, so the old rendering is never served for the new file.
        SetPlayback(material, playback.Value);

        if (request.ResetProgress)
        {
            foreach (var progress in await learningProgressRepository.ListAsync(x => x.MaterialId == id, cancellationToken))
                learningProgressRepository.Remove(progress);
        }

        try
        {
            await musicMaterialRepository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // The row still points at the old file, so the new one is the orphan; same cleanup as UploadAsync.
            await fileStorageService.DeleteAsync(newPublicId, isPrivate: true, CancellationToken.None);
            await DeletePlaybackAsync(playback.Value?.AudioPublicId, playback.Value?.NotesPublicId);
            throw;
        }

        // Best effort: nothing points at the old file any more, and a failed delete is logged by the storage service.
        await fileStorageService.DeleteAsync(oldPublicId, isPrivate: true, CancellationToken.None);
        await DeletePlaybackAsync(oldAudioId, oldNotesId);

        if (material.TargetSkillId is { } skillId)
            material.TargetSkill = await skillRepository.GetByIdAsync(skillId, cancellationToken);
        return Result<MusicMaterialDto>.Success(ToDto<MusicMaterialDto>(material));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var material = await musicMaterialRepository.GetByIdAsync(id, cancellationToken);
        if (material is not { IsActive: true }) return Result.Failure(ErrorCodes.MaterialNotFound);

        material.IsActive = false;
        await musicMaterialRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<PlaybackDto>> GetPlaybackAsync(Guid id, CancellationToken cancellationToken)
    {
        var material = await musicMaterialRepository.GetByIdAsync(id, cancellationToken);
        if (material is not { IsActive: true }) return Result<PlaybackDto>.Failure(ErrorCodes.MaterialNotFound);
        if (material.PlaybackAudioPublicId is null || material.PlaybackNotesPublicId is null || material.PlaybackDurationSeconds is null)
            return Result<PlaybackDto>.Failure(ErrorCodes.MaterialPlaybackUnavailable);

        var dto = mapper.Map<PlaybackDto>(material);
        dto.AudioUrl = fileStorageService.GetSignedUrl(material.PlaybackAudioPublicId);
        dto.NotesUrl = fileStorageService.GetSignedUrl(material.PlaybackNotesPublicId);
        return Result<PlaybackDto>.Success(dto);
    }

    /// <summary>
    /// Renders a MIDI upload and stores its playback files before the MIDI itself, so a file the renderer rejects
    /// never reaches storage. Any other file returns success with no playback files.
    /// </summary>
    private async Task<Result<PlaybackFiles?>> StorePlaybackAsync(
        FileContent file, PlaybackInstrument instrument, CancellationToken cancellationToken)
    {
        if (!MidiExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            return Result<PlaybackFiles?>.Success(null);

        var render = midiRenderer.Render(file.Content, instrument);
        // The MIDI itself is uploaded next from the same stream; IFormFile streams are seekable.
        file.Content.Position = 0;
        if (!render.IsSuccess) return Result<PlaybackFiles?>.Failure(render.Code!);

        using var audioContent = new MemoryStream(render.Value!.AudioWav);
        var audio = await fileStorageService.UploadAsync(audioContent, "audio.wav", PlaybackFolder, isPrivate: true, cancellationToken);
        if (!audio.IsSuccess) return Result<PlaybackFiles?>.Failure(audio.Code!);

        using var notesContent = new MemoryStream(render.Value.NotesJson);
        var notes = await fileStorageService.UploadAsync(notesContent, "notes.json", PlaybackFolder, isPrivate: true, cancellationToken);
        if (!notes.IsSuccess)
        {
            await DeletePlaybackAsync(audio.Value!.PublicId, null);
            return Result<PlaybackFiles?>.Failure(notes.Code!);
        }

        return Result<PlaybackFiles?>.Success(
            new PlaybackFiles(audio.Value!.PublicId, notes.Value!.PublicId, render.Value.DurationSeconds, instrument));
    }

    private static void SetPlayback(MusicMaterial material, PlaybackFiles? playback)
    {
        material.PlaybackAudioPublicId = playback?.AudioPublicId;
        material.PlaybackNotesPublicId = playback?.NotesPublicId;
        material.PlaybackDurationSeconds = playback?.DurationSeconds;
        material.PlaybackInstrument = playback?.Instrument;
    }

    // Best effort, like the other cleanups here: a failed delete is logged by the storage service.
    private async Task DeletePlaybackAsync(string? audioPublicId, string? notesPublicId)
    {
        if (audioPublicId is not null) await fileStorageService.DeleteAsync(audioPublicId, isPrivate: true, CancellationToken.None);
        if (notesPublicId is not null) await fileStorageService.DeleteAsync(notesPublicId, isPrivate: true, CancellationToken.None);
    }

    private sealed record PlaybackFiles(
        string AudioPublicId, string NotesPublicId, double DurationSeconds, PlaybackInstrument Instrument);

    private TDto ToDto<TDto>(MusicMaterial material) where TDto : MusicMaterialDto
    {
        var dto = mapper.Map<TDto>(material);
        dto.FileUrl = material.FilePublicId is null ? null : fileStorageService.GetSignedUrl(material.FilePublicId);
        return dto;
    }

    private async Task<Result<MusicMaterialDto>> AddLyricsTextAsync(
        UploadMusicMaterialRequest request, Skill? targetSkill, CancellationToken cancellationToken)
    {
        var material = mapper.Map<MusicMaterial>(request);
        material.Id = Guid.NewGuid();
        await musicMaterialRepository.AddAsync(material, cancellationToken);
        await musicMaterialRepository.SaveChangesAsync(cancellationToken);

        material.TargetSkill = targetSkill;
        return Result<MusicMaterialDto>.Success(ToDto<MusicMaterialDto>(material));
    }

    private PagedList<TDto> ToDtoPage<TDto>(PagedList<MusicMaterial> page) where TDto : MusicMaterialDto =>
        new(page.Items.Select(ToDto<TDto>).ToList(), page.PageNumber, page.PageSize, page.TotalCount);

    /// <summary>No skill id means the material is for everyone; otherwise the skill must exist and be active.</summary>
    private async Task<(Skill? Skill, string? Error)> GetTargetSkillAsync(Guid? skillId, CancellationToken cancellationToken)
    {
        if (skillId is null) return (null, null);

        var skill = await skillRepository.GetByIdAsync(skillId.Value, cancellationToken);
        if (skill is null) return (null, ErrorCodes.SkillNotFound);
        return skill.IsActive ? (skill, null) : (null, ErrorCodes.SkillInactive);
    }

    /// <summary>Extension and size come from the server-side upload; the client's Content-Type is never trusted.</summary>
    private static string? CheckFile(MaterialType materialType, FileContent? file)
    {
        if (file is null || file.Length == 0) return ErrorCodes.MaterialFileRequired;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions(materialType).Contains(extension)) return ErrorCodes.MaterialFileTypeNotAllowed;

        return file.Length > MaxFileSizeBytes ? ErrorCodes.MaterialFileTooLarge : null;
    }

    private static string[] AllowedExtensions(MaterialType materialType) => materialType switch
    {
        MaterialType.SampleAudio => AudioExtensions,
        MaterialType.Lyrics => LyricsExtensions,
        MaterialType.RehearsalMaterial => [.. DocumentExtensions, .. AudioExtensions],
        _ => DocumentExtensions,
    };

    // One storage folder per material type, e.g. harmonia/music-materials/sample-audio/{guid}.mp3.
    private static string FolderFor(MaterialType materialType) => materialType switch
    {
        MaterialType.SheetMusic => "music-materials/sheet-music",
        MaterialType.Lyrics => "music-materials/lyrics",
        MaterialType.SampleAudio => "music-materials/sample-audio",
        MaterialType.RehearsalMaterial => "music-materials/rehearsal-material",
        _ => throw new ArgumentOutOfRangeException(nameof(materialType), materialType, "Unknown material type."),
    };
}
