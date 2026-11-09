using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class DirectorTrainingProfile : Profile
{
    public DirectorTrainingProfile()
    {
        // The signed URL needs the storage service; DirectorTrainingService fills it after mapping.
        CreateMap<DirectorTraining, DirectorTrainingDto>()
            .ForMember(dest => dest.CertificateUrl, opt => opt.Ignore());
    }
}
