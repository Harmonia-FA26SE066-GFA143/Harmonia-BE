using System;
using System.Collections.Generic;

namespace Harmonia.Application.DTOs;

/// <summary>A Published event as the calling user sees it, with their own request, assignments and attendance (UC-04).</summary>
public class MemberEventDetailDto
{
    public Guid Id { get; set; }

    public DateOnly EventDate { get; set; }

    public TimeOnly Time { get; set; }

    public string? Title { get; set; }

    public string LocationName { get; set; } = string.Empty;

    public string? LiturgicalSeasonName { get; set; }

    public string? MassTypeName { get; set; }

    public string? CeremonyTypeName { get; set; }

    public string? SpecialRequirements { get; set; }

    /// <summary>Null when the caller was not asked to confirm.</summary>
    public MyParticipationDto? Participation { get; set; }

    /// <summary>Empty until the roster is finalized and the caller has been notified.</summary>
    public List<MyAssignmentDto> Assignments { get; set; } = [];

    public List<MemberRehearsalDetailDto> Rehearsals { get; set; } = [];
}
