using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class ReviewSongListRequest
{
    public ReviewDecision Decision { get; set; }

    public string? Notes { get; set; }
}