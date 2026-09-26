using System.Text.Json;
using System.Text.Json.Serialization;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Harmonia.Infrastructure.Data.Interceptors;

/// <summary>
/// On every save: stamps CreatedAt/By and UpdatedAt/By on <see cref="BaseAuditableEntity"/>, and writes an
/// <see cref="AuditLog"/> row for changes to the entity types in <see cref="AuditedTypes"/>. The log rows are
/// saved in the same transaction as the change they describe.
/// </summary>
public class AuditableEntityInterceptor(ICurrentUserService currentUser) : SaveChangesInterceptor
{
    private static readonly HashSet<Type> AuditedTypes =
    [
        typeof(User), typeof(Role), typeof(SystemSetting),
        typeof(SongList), typeof(SongListReview),
        typeof(ServiceRoster), typeof(RosterAssignment),
        typeof(MemberSkill),
        typeof(LiturgicalWeek), typeof(LiturgicalEvent)
    ];

    // Stamped by this interceptor, so they add nothing to a diff.
    private static readonly HashSet<string> AuditColumns =
    [
        nameof(BaseAuditableEntity.CreatedAt), nameof(BaseAuditableEntity.CreatedBy),
        nameof(BaseAuditableEntity.UpdatedAt), nameof(BaseAuditableEntity.UpdatedBy)
    ];

    // Routine bookkeeping that would flood the log (every login touches LastLoginAt).
    private static readonly HashSet<string> IgnoredProperties =
    [
        $"{nameof(User)}.{nameof(User.LastLoginAt)}"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();

        var now = DateTime.UtcNow;
        var userId = currentUser.UserId;

        // Build log rows before stamping, so the diff reflects only what the caller changed.
        var logs = context.ChangeTracker.Entries<BaseEntity>()
            .Where(e => AuditedTypes.Contains(e.Metadata.ClrType))
            .Select(e => BuildLog(e, userId, now))
            .OfType<AuditLog>()
            .ToList();

        foreach (var entry in context.ChangeTracker.Entries<BaseAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = userId;
                entry.Property(x => x.CreatedAt).IsModified = false;
                entry.Property(x => x.CreatedBy).IsModified = false;
            }
        }

        context.Set<AuditLog>().AddRange(logs);
    }

    private AuditLog? BuildLog(EntityEntry<BaseEntity> entry, Guid? userId, DateTime now)
    {
        var entityType = entry.Metadata.ClrType.Name;
        var properties = entry.Properties
            .Where(p => !AuditColumns.Contains(p.Metadata.Name)
                && !IgnoredProperties.Contains($"{entityType}.{p.Metadata.Name}")
                && !IsSensitive(p.Metadata.Name))
            .ToList();

        Dictionary<string, object?>? oldValues = null;
        Dictionary<string, object?>? newValues = null;
        string action;

        switch (entry.State)
        {
            case EntityState.Added:
                action = "Created";
                newValues = properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
                break;

            case EntityState.Deleted:
                action = "Deleted";
                oldValues = properties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
                break;

            case EntityState.Modified:
                action = "Updated";
                var changed = properties
                    .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
                    .ToList();
                if (changed.Count == 0)
                {
                    return null;
                }

                oldValues = changed.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
                newValues = changed.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
                break;

            default:
                return null;
        }

        return new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entry.Entity.Id,
            OldValue = oldValues is null ? null : JsonSerializer.Serialize(oldValues, JsonOptions),
            NewValue = newValues is null ? null : JsonSerializer.Serialize(newValues, JsonOptions),
            IpAddress = currentUser.IpAddress,
            CreatedAt = now
        };
    }

    // Name-based so that entities added later are protected too (03-security.md: no hashes or tokens in AuditLog).
    private static bool IsSensitive(string propertyName) =>
        propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase);
}
