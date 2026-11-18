using Harmonia.Application.Common.Models;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/member-registrations: paging and a keyword over full name or email.</summary>
public class SearchMemberRegistrationsRequest : PagingRequest
{
    public string? Keyword { get; set; }
}
