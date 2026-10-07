using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Mappings;

public class PracticeAssignmentProfile : Profile
{
    public PracticeAssignmentProfile()
    {
        CreateMap<PracticeAssignment, PracticeAssignmentDto>()
            .ForMember(dest => dest.SkillIds, opt => opt.MapFrom(src => src.Targets
                .Where(t => t.TargetType == TargetType.Skill)
                .Select(t => t.SkillId!.Value)))
            .ForMember(dest => dest.MemberIds, opt => opt.MapFrom(src => src.Targets
                .Where(t => t.TargetType == TargetType.Member)
                .Select(t => t.MemberId!.Value)));
    }
}
