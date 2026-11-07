using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Mappings;

public class MusicMaterialProfile : Profile
{
    public MusicMaterialProfile()
    {
        ValueTransformers.Add<string>(value => string.IsNullOrWhiteSpace(value) ? null! : value.Trim());

        // FileUrl is a signed URL built by MusicMaterialService through IFileStorageService.
        CreateMap<MusicMaterial, MusicMaterialDto>()
            .ForMember(dest => dest.TargetSkillName, opt => opt.MapFrom(src => src.TargetSkill != null ? src.TargetSkill.Name : null))
            .ForMember(dest => dest.FileUrl, opt => opt.Ignore());

        // LearningProgresses must hold only the calling member's row (see GetActiveForMemberAsync).
        CreateMap<MusicMaterial, MusicMaterialDetailDto>()
            .IncludeBase<MusicMaterial, MusicMaterialDto>()
            .ForMember(dest => dest.LearningStatus, opt => opt.MapFrom(src =>
                src.LearningProgresses.Select(p => (LearningStatus?)p.Status).FirstOrDefault() ?? LearningStatus.NotStarted))
            .ForMember(dest => dest.LearningUpdatedAt, opt => opt.MapFrom(src =>
                src.LearningProgresses.Select(p => (DateTime?)p.UpdatedAt).FirstOrDefault()));

        // Signed URLs are built by MusicMaterialService; only called once PlaybackDurationSeconds is set.
        CreateMap<MusicMaterial, PlaybackDto>()
            .ForMember(dest => dest.DurationSeconds, opt => opt.MapFrom(src => src.PlaybackDurationSeconds!.Value))
            .ForMember(dest => dest.Instrument, opt => opt.MapFrom(src => src.PlaybackInstrument ?? PlaybackInstrument.Piano))
            .ForMember(dest => dest.AudioUrl, opt => opt.Ignore())
            .ForMember(dest => dest.NotesUrl, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.Ignore())
            .ForMember(dest => dest.SchemaVersion, opt => opt.Ignore());

        // Signed URLs are built by MusicMaterialService; only called once the score and its playback are saved.
        CreateMap<MusicMaterial, InteractiveScoreDto>()
            .ForMember(dest => dest.Revision, opt => opt.MapFrom(src => src.ScoreRevision ?? 1))
            .ForMember(dest => dest.ScoreFormat, opt => opt.MapFrom(src =>
                src.FileName != null && src.FileName.EndsWith(".mxl", StringComparison.OrdinalIgnoreCase) ? "mxl" : "musicxml"))
            .ForMember(dest => dest.DurationSeconds, opt => opt.MapFrom(src => src.PlaybackDurationSeconds!.Value))
            .ForMember(dest => dest.Instrument, opt => opt.MapFrom(src => src.PlaybackInstrument ?? PlaybackInstrument.Piano))
            .ForMember(dest => dest.AudioSha256, opt => opt.MapFrom(src => src.PlaybackAudioSha256))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => src.UpdatedAt ?? src.CreatedAt))
            .ForMember(dest => dest.ScoreUrl, opt => opt.Ignore())
            .ForMember(dest => dest.AudioUrl, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.Ignore());

        CreateMap<UploadMusicMaterialRequest, MusicMaterial>();
        CreateMap<UpdateMusicMaterialRequest, MusicMaterial>();
    }
}
