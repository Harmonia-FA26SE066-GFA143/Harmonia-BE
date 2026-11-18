using Harmonia.Application.Common.Models;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/choir-join-requests: paging and optional filters.</summary>
public class SearchChoirJoinRequestsRequest : PagingRequest
{
    public ChoirJoinRequestStatus? Status { get; set; }

    public Guid? ChoirId { get; set; }

    public bool? IsInvitation { get; set; }
}
