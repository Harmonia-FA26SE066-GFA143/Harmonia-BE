using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class SongListProfile : Profile
{
    public SongListProfile()
    {
        CreateMap<SongList, SongListDto>();
    }
}