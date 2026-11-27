using Harmonia.Application.Common.Models;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/director-nominations: paging and an optional status filter.</summary>
public class SearchDirectorNominationsRequest : PagingRequest
{
    public DirectorNominationStatus? Status { get; set; }
}
