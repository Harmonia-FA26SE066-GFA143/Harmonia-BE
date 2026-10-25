namespace Harmonia.Application.DTOs;

public class LiturgicalDayDto
{
    public DateOnly Date { get; set; }

    /// <summary>English, as the calendar returns it; show <see cref="CelebrationNameVi"/> instead.</summary>
    public string CelebrationName { get; set; } = string.Empty;

    /// <summary>Solemnity, Feast, Obligatory Memorial, Optional Memorial; null for a weekday.</summary>
    public string? Rank { get; set; }

    /// <summary>Advent, Christmas, Lent, Easter or OrdinaryTime.</summary>
    public string? SeasonName { get; set; }

    /// <summary>Vietnamese name from the Admin's translations or a known pattern; the English name when neither has it.</summary>
    public string CelebrationNameVi { get; set; } = string.Empty;

    public string? RankVi { get; set; }

    public string? SeasonNameVi { get; set; }
}