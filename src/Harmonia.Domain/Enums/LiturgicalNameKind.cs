namespace Harmonia.Domain.Enums;

/// <summary>
/// Which field of LiturgicalDay a translated name belongs to; the same English word can mean two things, e.g. the
/// celebration "Christmas" and the season "Christmas".
/// </summary>
public enum LiturgicalNameKind
{
    Celebration,
    Rank,
    Season
}
