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

        CreateMap<UploadMusicMaterialRequest, MusicMaterial>();
        CreateMap<UpdateMusicMaterialRequest, MusicMaterial>();
    }
}
