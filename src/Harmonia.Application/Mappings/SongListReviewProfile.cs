using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class SongListReviewProfile : Profile
{
    public SongListReviewProfile()
    {
        CreateMap<SongListReview, SongListReviewDto>();
    }
}
