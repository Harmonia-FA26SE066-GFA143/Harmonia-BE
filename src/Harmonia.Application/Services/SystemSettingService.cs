using System.Globalization;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class SystemSettingService(IGenericRepository<SystemSetting> systemSettings) : ISystemSettingService
{
    public async Task<Result<List<SystemSettingDto>>> GetAllAsync(CancellationToken cancellationToken) =>
        Result<List<SystemSettingDto>>.Success(ToDtos(await systemSettings.ListAsync(x => true, cancellationToken)));

    public async Task<Result<List<SystemSettingDto>>> GetPublicAsync(bool isSignedIn, CancellationToken cancellationToken)
    {
        // The public flags live in code, not in the table, so a row cannot be exposed by editing the database.
        var publicKeys = SystemSettingKeys.All
            .Where(x => x.IsPublic && (isSignedIn || x.IsAnonymous))
            .Select(x => x.Key)
            .ToList();
        var rows = await systemSettings.ListAsync(x => publicKeys.Contains(x.Key), cancellationToken);

        return Result<List<SystemSettingDto>>.Success(ToDtos(rows));
    }

    public async Task<Result<SystemSettingDto>> UpdateAsync(
        Guid id, UpdateSystemSettingRequest request, CancellationToken cancellationToken)
    {
        var setting = await systemSettings.GetByIdAsync(id, cancellationToken);
        if (setting is null) return Result<SystemSettingDto>.Failure(ErrorCodes.SettingNotFound);

        var previous = setting.Value;
        setting.SetValue(request.Value);

        // SetValue left a parsable integer behind, so only the range remains to check.
        var definition = SystemSettingKeys.Find(setting.Key);
        if (setting.DataType == SettingDataType.Int && definition is not null)
        {
            var number = int.Parse(setting.Value, CultureInfo.InvariantCulture);
            if (number < definition.MinValue || number > definition.MaxValue)
            {
                // The row is tracked; put the old value back so nothing later in the request can save the bad one.
                setting.Value = previous;
                return Result<SystemSettingDto>.Failure(ErrorCodes.SettingValueOutOfRange);
            }
        }

        await systemSettings.SaveChangesAsync(cancellationToken);
        return Result<SystemSettingDto>.Success(ToDto(setting));
    }

    public async Task<int> GetIntAsync(string key, CancellationToken cancellationToken) =>
        int.TryParse(await GetValueAsync(key, cancellationToken), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : int.Parse(DefaultOf(key), CultureInfo.InvariantCulture);

    public async Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken) =>
        bool.TryParse(await GetValueAsync(key, cancellationToken), out var value) ? value : bool.Parse(DefaultOf(key));

    private async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken) =>
        (await systemSettings.ListAsync(x => x.Key == key, cancellationToken)).FirstOrDefault()?.Value;

    // An undefined key is a programming error, not something a request can cause, so it may throw.
    private static string DefaultOf(string key) =>
        (SystemSettingKeys.Find(key) ?? throw new ArgumentException($"Unknown system setting key '{key}'.", nameof(key)))
        .DefaultValue;

    private static List<SystemSettingDto> ToDtos(IEnumerable<SystemSetting> rows) =>
        rows.Select(ToDto).OrderBy(x => x.Order).ThenBy(x => x.Key, StringComparer.Ordinal).ToList();

    private static SystemSettingDto ToDto(SystemSetting setting)
    {
        var definition = SystemSettingKeys.Find(setting.Key);
        return new SystemSettingDto
        {
            Id = setting.Id,
            Key = setting.Key,
            Value = setting.Value,
            DataType = setting.DataType,
            // A row whose key left the code is labelled by its key and goes last.
            DisplayName = definition?.DisplayName ?? setting.Key,
            Group = definition?.Group ?? string.Empty,
            Order = definition is null ? int.MaxValue : SystemSettingKeys.All.ToList().IndexOf(definition),
            Description = setting.Description,
            IsPublic = definition?.IsPublic ?? false,
            MinValue = definition?.MinValue,
            MaxValue = definition?.MaxValue,
        };
    }
}
