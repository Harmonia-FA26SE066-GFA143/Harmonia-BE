namespace Harmonia.Application.DTOs;

/// <summary>One liturgical season / Mass type / ceremony type / theme a song is classified under.</summary>
public class SongClassificationSummaryDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
