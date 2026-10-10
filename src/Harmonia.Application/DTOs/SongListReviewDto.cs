using Harmonia.Domain.Enums;
using System;

namespace Harmonia.Application.DTOs;

public class SongListReviewDto
{
    public Guid Id { get; set; }

    public Guid ReviewerId { get; set; }

    public ReviewDecision Decision { get; set; }

    public string? Notes { get; set; }

    public DateTime ReviewedAt { get; set; }
}