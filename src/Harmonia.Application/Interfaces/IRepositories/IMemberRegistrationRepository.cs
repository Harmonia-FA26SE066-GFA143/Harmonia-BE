using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IMemberRegistrationRepository : IGenericRepository<MemberRegistration>
{
    /// <summary>Tracked, so a repeated submission updates the waiting row instead of adding a second one.</summary>
    Task<MemberRegistration?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>Newest first; <paramref name="keyword"/> matches the full name or the email.</summary>
    Task<PagedList<MemberRegistration>> SearchAsync(
        string? keyword, SearchMemberRegistrationsRequest filter, CancellationToken cancellationToken);
}
