using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class EventCategoryProfile : Profile
{
    public EventCategoryProfile()
    {
        CreateMap<EventCategory, LookupDto>();
    }
}
