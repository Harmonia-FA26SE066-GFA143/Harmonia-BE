using Harmonia.Application.Common.Models;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/director-notes.</summary>
public class SearchDirectorNotesRequest : PagingRequest
{
    public Guid? EventId { get; set; }

    /// <summary>Narrows to the conversation between the caller and this user.</summary>
    public Guid? WithUserId { get; set; }
}
