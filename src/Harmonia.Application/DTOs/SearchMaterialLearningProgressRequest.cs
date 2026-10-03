using Harmonia.Application.Common.Models;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/music-materials/{id}/learning-progress: paging plus an optional status filter.</summary>
public class SearchMaterialLearningProgressRequest : PagingRequest
{
    /// <summary>NotStarted matches members who have never marked the material.</summary>
    public LearningStatus? Status { get; set; }
}
