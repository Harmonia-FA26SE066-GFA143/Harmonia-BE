using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class SkillCategoryProfile : Profile
{
    public SkillCategoryProfile()
    {
        CreateMap<SkillCategory, LookupDto>();
    }
}
