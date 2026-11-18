namespace Harmonia.Application.DTOs;

public class TimedLyricLineRequest
{
    public int Order { get; set; }

    public string Text { get; set; } = string.Empty;

    public double StartSeconds { get; set; }

    public double EndSeconds { get; set; }
}
