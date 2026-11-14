using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class EventPersonnelRequirementProfile : Profile
{
    public EventPersonnelRequirementProfile()
    {
        // SkillName and SkillCategoryId flatten from Skill.Name and Skill.CategoryId.
        CreateMap<EventPersonnelRequirement, EventPersonnelRequirementDto>();
    }
}
