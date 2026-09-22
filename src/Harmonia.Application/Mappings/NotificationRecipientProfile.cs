using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class NotificationRecipientProfile : Profile
{
    public NotificationRecipientProfile()
    {
        // Used for the list endpoint, where read state comes from the recipient row.
        CreateMap<NotificationRecipient, NotificationDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.NotificationId))
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Notification.Type))
            .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Notification.Title))
            .ForMember(dest => dest.Content, opt => opt.MapFrom(src => src.Notification.Content))
            .ForMember(dest => dest.ReferenceType, opt => opt.MapFrom(src => src.Notification.ReferenceType))
            .ForMember(dest => dest.ReferenceId, opt => opt.MapFrom(src => src.Notification.ReferenceId))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.Notification.CreatedAt));
    }
}
