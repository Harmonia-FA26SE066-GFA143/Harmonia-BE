using Google.Apis.Auth;
using Harmonia.Application.Interfaces.IServices;
using Microsoft.Extensions.Options;

namespace Harmonia.Infrastructure.ExternalServices;

public class GoogleTokenValidator(IOptions<GoogleAuthOptions> options) : IGoogleTokenValidator
{
    private readonly GoogleJsonWebSignature.ValidationSettings _settings = new()
    {
        Audience = options.Value.GetClientIds(),
    };

    public async Task<string?> GetVerifiedEmailAsync(string idToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Checks signature against Google's published keys, issuer, audience and expiry.
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, _settings);
            return payload.EmailVerified ? payload.Email : null;
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
