namespace Harmonia.Application.DTOs;

/// <summary>Body of the Parish Priest's reject, cancel and waive-training actions: the reason is required.</summary>
public class ReviewDirectorNominationRequest
{
    public string Note { get; set; } = string.Empty;
}
