using Harmonia.Domain.Entities;

namespace Harmonia.Domain.ValueObjects;

/// <summary>One line of <see cref="TimedLyrics"/>; owned by it, no id of its own.</summary>
public class TimedLyricLine
{
    public int Order { get; set; }

    public string Text { get; set; } = string.Empty;

    public double StartSeconds { get; set; }

    public double EndSeconds { get; set; }
}
