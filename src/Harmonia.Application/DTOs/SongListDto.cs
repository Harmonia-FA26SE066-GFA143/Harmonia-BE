using Harmonia.Domain.Enums;
using System;
using System.Collections.Generic;

namespace Harmonia.Application.DTOs;

public class SongListDto
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public int Version { get; set; }

    public SongListStatus Status { get; set; }

    public Guid ProposedBy { get; set; }

    public Guid? PreviousVersionId { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? DecidedAt { get; set; }

    public List<SongListItemDto> Items { get; set; } = [];

    public List<SongListReviewDto> Reviews { get; set; } = [];
}