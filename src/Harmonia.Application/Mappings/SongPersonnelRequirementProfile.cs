using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class SongPersonnelRequirementProfile : Profile
{
    public SongPersonnelRequirementProfile()
    {
        // SkillName and SkillCategoryId flatten from Skill.Name and Skill.CategoryId.
        CreateMap<SongPersonnelRequirement, SongPersonnelRequirementDto>();
    }
}
