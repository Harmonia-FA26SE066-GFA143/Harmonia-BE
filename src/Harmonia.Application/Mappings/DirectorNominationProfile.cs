using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class DirectorNominationProfile : Profile
{
    public DirectorNominationProfile()
    {
        CreateMap<DirectorNomination, DirectorNominationDto>()
            .ForMember(dest => dest.MemberUserId, opt => opt.MapFrom(src => src.Member.UserId))
            .ForMember(dest => dest.MemberFullName, opt => opt.MapFrom(src => src.Member.User.FullName))
            .ForMember(dest => dest.NominatedByFullName, opt => opt.MapFrom(src => src.NominatedBy.FullName))
            .ForMember(dest => dest.ProposedChoirName,
                opt => opt.MapFrom(src => src.ProposedChoir == null ? null : src.ProposedChoir.Name))
            .ForMember(dest => dest.AppointedChoirName,
                opt => opt.MapFrom(src => src.AppointedChoir == null ? null : src.AppointedChoir.Name));

        CreateMap<DirectorNomination, DirectorNominationDetailDto>()
            .IncludeBase<DirectorNomination, DirectorNominationDto>()
            .ForMember(dest => dest.Stats, opt => opt.Ignore());
    }
}
