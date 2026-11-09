namespace Harmonia.Application.DTOs;

public class CreateDirectorNominationRequest
{
    public Guid MemberId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid? ProposedChoirId { get; set; }
}
