using Harmonia.Application.Common.Models;

namespace Harmonia.Application.Interfaces.IServices;

public interface IEmailSender
{
    /// <summary>Sends one HTML email. Returns EXTERNAL_EMAIL_FAILED instead of throwing when the provider fails.</summary>
    Task<Result> SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken);
}
