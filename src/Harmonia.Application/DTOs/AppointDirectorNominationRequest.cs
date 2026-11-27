namespace Harmonia.Application.DTOs;

public class AppointDirectorNominationRequest
{
    /// <summary>The choir the new director takes over; its current director, if any, is replaced.</summary>
    public Guid ChoirId { get; set; }
}
