using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// The settings of <c>SystemSettingKeys</c> (UC-33 / FE-51): the Admin edits them, every role reads the public
/// ones, and other services read the values that steer their rules.
/// </summary>
public interface ISystemSettingService
{
    /// <summary>Every setting, in the order of the settings page. Admin only.</summary>
    Task<Result<List<SystemSettingDto>>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The settings marked public for a signed-in caller; for a visitor who has not signed in, only those also
    /// marked anonymous. In the order of the settings page.
    /// </summary>
    Task<Result<List<SystemSettingDto>>> GetPublicAsync(bool isSignedIn, CancellationToken cancellationToken);

    /// <summary>
    /// SETTING_NOT_FOUND for an unknown id, SETTING_VALUE_OUT_OF_RANGE for an Int outside the key's range.
    /// A value that is not of the setting's type throws SettingValueTypeMismatchException.
    /// </summary>
    Task<Result<SystemSettingDto>> UpdateAsync(Guid id, UpdateSystemSettingRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Current value of an Int key, for the service whose rule it steers. Falls back to the key's default when
    /// the row is missing or unreadable, so a damaged setting never stops that service.
    /// </summary>
    Task<int> GetIntAsync(string key, CancellationToken cancellationToken);

    /// <summary>Current value of a Bool key; same fallback as <see cref="GetIntAsync"/>.</summary>
    Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken);
}
