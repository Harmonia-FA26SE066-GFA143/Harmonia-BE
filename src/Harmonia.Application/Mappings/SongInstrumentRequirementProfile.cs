using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class SongInstrumentRequirementProfile : Profile
{
    public SongInstrumentRequirementProfile()
    {
        // SkillName flattens from Skill.Name.
        CreateMap<SongInstrumentRequirement, SongInstrumentRequirementDto>();
    }
}
