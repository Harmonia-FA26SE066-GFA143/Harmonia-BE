namespace Harmonia.Application.DTOs;

/// <summary>Every classification of one song (UC-21 / FE-29). Inactive lookups still show, so history stays readable.</summary>
public class SongClassificationDto
{
    public Guid SongId { get; set; }

    public List<SongClassificationSummaryDto> LiturgicalSeasons { get; set; } = [];

    public List<SongClassificationSummaryDto> MassTypes { get; set; } = [];

    public List<SongClassificationSummaryDto> CeremonyTypes { get; set; } = [];

    public List<SongClassificationSummaryDto> SongThemes { get; set; } = [];

    public List<SongVocalRequirementDto> VocalRequirements { get; set; } = [];

    public List<SongInstrumentRequirementDto> InstrumentRequirements { get; set; } = [];
}
