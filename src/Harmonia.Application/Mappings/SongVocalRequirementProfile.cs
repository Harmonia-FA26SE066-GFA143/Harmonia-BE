using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class SongVocalRequirementProfile : Profile
{
    public SongVocalRequirementProfile()
    {
        // SkillName flattens from Skill.Name.
        CreateMap<SongVocalRequirement, SongVocalRequirementDto>();
    }
}
