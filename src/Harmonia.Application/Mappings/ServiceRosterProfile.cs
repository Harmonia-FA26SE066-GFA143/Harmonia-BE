using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class ServiceRosterProfile : Profile
{
    public ServiceRosterProfile()
    {
        // The service fills both lists: Assignments from the Active lines only, Shortages computed on the fly.
        CreateMap<ServiceRoster, ServiceRosterDto>()
            .ForMember(d => d.Assignments, o => o.Ignore())
            .ForMember(d => d.Shortages, o => o.Ignore());
    }
}
