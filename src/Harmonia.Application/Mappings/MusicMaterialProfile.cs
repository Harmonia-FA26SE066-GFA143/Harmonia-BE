using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

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

        CreateMap<UploadMusicMaterialRequest, MusicMaterial>();
        CreateMap<UpdateMusicMaterialRequest, MusicMaterial>();
    }
}
