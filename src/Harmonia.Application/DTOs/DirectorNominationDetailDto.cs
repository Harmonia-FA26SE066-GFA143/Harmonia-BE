namespace Harmonia.Application.DTOs;

/// <summary>One nomination with the member's track record, computed when it is read.</summary>
public class DirectorNominationDetailDto : DirectorNominationDto
{
    public DirectorNominationStatsDto Stats { get; set; } = new();
}
