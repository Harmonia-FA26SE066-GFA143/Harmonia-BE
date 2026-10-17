using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class MemberSkillProfile : Profile
{
    public MemberSkillProfile()
    {
        CreateMap<MemberSkill, MemberSkillSummaryDto>()
            .ForMember(dest => dest.SkillName, opt => opt.MapFrom(src => src.Skill.Name))
            .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.Skill.CategoryId))
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Skill.Category.Name));

        CreateMap<MemberSkill, MemberSkillDto>()
            .ForMember(dest => dest.SkillName, opt => opt.MapFrom(src => src.Skill.Name))
            .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.Skill.CategoryId))
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Skill.Category.Name));

        CreateMap<MemberSkill, MemberSkillDetailDto>()
            .IncludeBase<MemberSkill, MemberSkillDto>()
            .ForMember(dest => dest.MemberFullName, opt => opt.MapFrom(src => src.Member.User.FullName));
    }
}
