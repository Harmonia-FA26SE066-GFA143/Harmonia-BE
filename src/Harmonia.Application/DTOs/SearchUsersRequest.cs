using Harmonia.Application.Common.Models;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/users: paging, a keyword over email, and optional role / active filters (AND).</summary>
public class SearchUsersRequest : PagingRequest
{
    public string? Keyword { get; set; }

    public string? RoleName { get; set; }

    public bool? IsActive { get; set; }
}
