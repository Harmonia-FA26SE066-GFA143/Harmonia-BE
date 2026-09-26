using brevo_csharp.Api;
using brevo_csharp.Client;
using brevo_csharp.Model;
using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmonia.Infrastructure.ExternalServices;

public class BrevoEmailSender : IEmailSender
{
    private readonly TransactionalEmailsApi _api;
    private readonly SendSmtpEmailSender _sender;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(IOptions<BrevoOptions> options, ILogger<BrevoEmailSender> logger)
    {
        var brevo = options.Value;

        // Per-instance configuration instead of the SDK's static Configuration.Default,
        // so the key never lives in global state.
        _api = new TransactionalEmailsApi(new brevo_csharp.Client.Configuration
        {
            ApiKey = new Dictionary<string, string> { ["api-key"] = brevo.ApiKey }
        });
        _sender = new SendSmtpEmailSender(name: brevo.FromName, email: brevo.FromEmail);
        _logger = logger;
    }

    public async Task<Result> SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var message = new SendSmtpEmail(
            sender: _sender,
            to: [new SendSmtpEmailTo(email: to)],
            subject: subject,
            htmlContent: htmlBody);

        try
        {
            // The SDK takes no CancellationToken; WaitAsync stops us waiting, the HTTP call itself still completes.
            await _api.SendTransacEmailAsync(message).WaitAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException)
        {
            // Recipient address is deliberately left out of the log.
            _logger.LogWarning(ex, "Brevo failed to send email with subject {Subject}", subject);
            return Result.Failure(ErrorCodes.ExternalEmailFailed);
        }
    }
}
