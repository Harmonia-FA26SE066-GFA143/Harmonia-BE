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
    IFileStorageService fileStorageService,
    IMapper mapper) : IMusicMaterialService
{
    public const long MaxFileSizeBytes = 20 * 1024 * 1024;

    // Matches MusicMaterialConfiguration.FileName.
    private const int MaxFileNameLength = 200;

    private static readonly string[] DocumentExtensions = [".pdf", ".png", ".jpg"];

    private static readonly string[] AudioExtensions = [".mp3", ".m4a", ".wav"];

    public async Task<Result<MusicMaterialDto>> UploadAsync(
        UploadMusicMaterialRequest request, FileContent? file, CancellationToken cancellationToken)
    {
        var song = await songRepository.GetByIdAsync(request.SongId, cancellationToken);
        if (song is not { IsActive: true }) return Result<MusicMaterialDto>.Failure(ErrorCodes.SongNotFound);

        var (targetSkill, skillError) = await GetTargetSkillAsync(request.TargetSkillId, cancellationToken);
        if (skillError is not null) return Result<MusicMaterialDto>.Failure(skillError);

        if (CheckFile(request.MaterialType, file) is { } fileError)
            return Result<MusicMaterialDto>.Failure(fileError);

        var upload = await fileStorageService.UploadAsync(
            file!.Content, file.FileName, FolderFor(request.MaterialType), isPrivate: true, cancellationToken);
        if (!upload.IsSuccess) return Result<MusicMaterialDto>.Failure(upload.Code!);

        var publicId = upload.Value!.PublicId;
        var fileName = Path.GetFileName(file.FileName);
        var material = mapper.Map<MusicMaterial>(request);
        material.Id = Guid.NewGuid();
        material.FilePublicId = publicId;
        // Keep the tail so the extension survives truncation.
        material.FileName = fileName.Length > MaxFileNameLength ? fileName[^MaxFileNameLength..] : fileName;
        material.FileSizeBytes = file.Length;

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
            throw;
        }

        material.TargetSkill = targetSkill;
        return Result<MusicMaterialDto>.Success(ToDto(material));
    }

    public async Task<Result<PagedList<MusicMaterialDto>>> GetBySongAsync(
        Guid songId, PagingRequest paging, CancellationToken cancellationToken)
    {
        var song = await songRepository.GetByIdAsync(songId, cancellationToken);
        if (song is not { IsActive: true }) return Result<PagedList<MusicMaterialDto>>.Failure(ErrorCodes.SongNotFound);

        var page = await musicMaterialRepository.GetActiveBySongAsync(songId, paging, cancellationToken);

        return Result<PagedList<MusicMaterialDto>>.Success(new PagedList<MusicMaterialDto>(
            page.Items.Select(ToDto).ToList(), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<MusicMaterialDto>> UpdateAsync(
        Guid id, UpdateMusicMaterialRequest request, CancellationToken cancellationToken)
    {
        var material = await musicMaterialRepository.GetByIdAsync(id, cancellationToken);
        if (material is not { IsActive: true }) return Result<MusicMaterialDto>.Failure(ErrorCodes.MaterialNotFound);

        var (targetSkill, skillError) = await GetTargetSkillAsync(request.TargetSkillId, cancellationToken);
        if (skillError is not null) return Result<MusicMaterialDto>.Failure(skillError);

        mapper.Map(request, material);
        await musicMaterialRepository.SaveChangesAsync(cancellationToken);

        material.TargetSkill = targetSkill;
        return Result<MusicMaterialDto>.Success(ToDto(material));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var material = await musicMaterialRepository.GetByIdAsync(id, cancellationToken);
        if (material is not { IsActive: true }) return Result.Failure(ErrorCodes.MaterialNotFound);

        material.IsActive = false;
        await musicMaterialRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private MusicMaterialDto ToDto(MusicMaterial material)
    {
        var dto = mapper.Map<MusicMaterialDto>(material);
        dto.FileUrl = fileStorageService.GetSignedUrl(material.FilePublicId);
        return dto;
    }

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
