using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class SongProfile : Profile
{
    public SongProfile()
    {
        // Trim every string and turn blanks into null, so "" and null compare equal in the
        // title + composer duplicate check. Title cannot go null: the validators reject blank titles.
        ValueTransformers.Add<string>(value => string.IsNullOrWhiteSpace(value) ? null! : value.Trim());

        CreateMap<Song, SongDto>();
        CreateMap<CreateSongRequest, Song>();
        CreateMap<UpdateSongRequest, Song>();
    }
}
