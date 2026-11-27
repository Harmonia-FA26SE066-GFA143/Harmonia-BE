namespace Harmonia.Application.DTOs;

/// <summary>Body of PUT api/practice-assignments/{id}: the text and due date only; recipients and materials stay.</summary>
public class UpdatePracticeAssignmentRequest
{
    public string Title { get; set; } = string.Empty;

    public string? Instruction { get; set; }

    public DateTime DueDate { get; set; }
}
