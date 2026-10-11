using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class AuditLogProfile : Profile
{
    public AuditLogProfile()
    {
        // UserFullName and UserEmail flatten from AuditLog.User by name; both are null without a user.
        CreateMap<AuditLog, AuditLogDto>();
    }
}
