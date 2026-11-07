namespace Harmonia.Application.Interfaces.IServices;

public interface IGoogleTokenValidator
{
    /// <summary>
    /// Verifies a Google ID token and returns its email, or null when the token is invalid,
    /// issued for another client, expired, or carries an unverified email.
    /// </summary>
    Task<string?> GetVerifiedEmailAsync(string idToken, CancellationToken cancellationToken);
}
