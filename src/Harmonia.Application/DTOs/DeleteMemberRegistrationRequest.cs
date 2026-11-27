namespace Harmonia.Application.DTOs;

/// <summary>Body of DELETE api/member-registrations/{id}: the Admin turns the request down and says why.</summary>
public class DeleteMemberRegistrationRequest
{
    /// <summary>Sent as-is in the email to the applicant.</summary>
    public string Reason { get; set; } = string.Empty;
}
