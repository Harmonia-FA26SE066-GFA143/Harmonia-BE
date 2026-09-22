using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class NotificationProfile : Profile
{
    public NotificationProfile()
    {
        // Used for the real-time push: the notification was just created, so nobody has read it.
        CreateMap<Notification, NotificationDto>()
            .ForMember(dest => dest.IsRead, opt => opt.Ignore())
            .ForMember(dest => dest.ReadAt, opt => opt.Ignore());
    }
}
