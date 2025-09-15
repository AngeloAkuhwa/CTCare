using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

using CTCare.Domain.Entities;
using CTCare.Shared.Utilities;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace CTCare.Infrastructure.Persistence;

public sealed class AuditSaveChangesInterceptor(IHttpContextAccessor http): SaveChangesInterceptor
{
    // Buffer pending audit rows per DbContext instance to avoid recursion
    private readonly ConditionalWeakTable<DbContext, List<AuditLog>> _pending
        = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var ctx = eventData.Context;
        if (ctx is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        // Build audit entries from the current ChangeTracker
        var list = BuildAuditLogs(ctx);
        if (list.Count > 0)
        {
            _pending.Remove(ctx);
            _pending.AddOrUpdate(ctx, list);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var ctx = eventData.Context;
        if (ctx is null)
        {
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        if (_pending.TryGetValue(ctx, out var logs) && logs.Count > 0)
        {
            try
            {
                // Use the same context to persist logs AFTER the main save
                ctx.Set<AuditLog>().AddRange(logs);
                await ctx.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Don't break primary transaction if audit fails; just log
                try
                {
                    var logger = ctx.GetService<ILogger<AuditSaveChangesInterceptor>>();
                    logger.LogError(ex, "Failed to persist audit logs ({Count}).", logs.Count);
                }
                catch
                {
                    SentrySdk.CaptureException(ex);
                    await SentrySdk.FlushAsync(TimeSpan.FromSeconds(2));
                }
            }
            finally
            {
                _pending.Remove(ctx);
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        var ctx = eventData.Context;
        if (ctx is not null)
        {
            _pending.Remove(ctx);
        }

        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    // -------------------------------------------------------
    // Core: translate ChangeTracker entries => AuditLog rows
    // -------------------------------------------------------
    private List<AuditLog> BuildAuditLogs(DbContext ctx)
    {
        var now = DateTimeOffset.UtcNow;
        var http1 = http.HttpContext;
        var (actorId, actorName) = GetActor(http1?.User);
        var metaJson = BuildMetadata(http1);

        var logs = new List<AuditLog>();

        foreach (var entry in ctx.ChangeTracker.Entries().Where(ShouldAudit))
        {
            var entityName = entry.Metadata.DisplayName();
            var entityId = GetPrimaryKey(entry);

            JsonDocument? oldDoc = null;
            JsonDocument? newDoc = null;
            string[]? changedColumns = null;
            string action;

            switch (entry.State)
            {
                case EntityState.Added:
                    action = "Create";
                    newDoc = ValuesToJson(entry.CurrentValues);
                    break;

                case EntityState.Modified:
                    action = "Update";
                    oldDoc = ValuesToJson(entry.OriginalValues);
                    newDoc = ValuesToJson(entry.CurrentValues);
                    changedColumns = GetChangedPropertyNames(entry).ToArray();
                    break;

                case EntityState.Deleted:
                    action = "Delete";
                    oldDoc = ValuesToJson(entry.OriginalValues);
                    break;

                default:
                    continue;
            }

            logs.Add(new AuditLog
            {
                Id = SequentialGuid.NewGuid(),
                ActorId = actorId,
                ActorName = actorName,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                TimestampUtc = now,
                OldValues = oldDoc,
                NewValues = newDoc,
                ChangedColumns = changedColumns,
                Metadata = metaJson
            });
        }

        return logs;
    }

    private static bool ShouldAudit(EntityEntry e)
    {
        // Ignore audit table itself + unchanged/detached
        if (e.Entity is AuditLog)
        {
            return false;
        }

        return e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;
    }

    private static Guid GetPrimaryKey(EntityEntry entry)
    {
        // Expect a Guid key named "Id" (your BaseEntity has it)
        var keyName = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault()?.Name ?? "Id";
        var value = entry.Property(keyName).CurrentValue ?? entry.Property(keyName).OriginalValue;
        return value is Guid g ? g : Guid.Empty;
    }

    private static IEnumerable<string> GetChangedPropertyNames(EntityEntry entry) =>
        entry.Properties
             .Where(p => p.IsModified && !p.Metadata.IsPrimaryKey())
             .Select(p => p.Metadata.Name);

    private static JsonDocument? ValuesToJson(PropertyValues? values)
    {
        if (values is null)
        {
            return null;
        }

        var dict = values.Properties.ToDictionary(
            p => p.Name,
            p => values[p.Name]
        );
        return JsonSerializer.SerializeToDocument(dict, JsonOpts);
    }

    private static string? BuildMetadata(HttpContext? http)
    {
        if (http is null)
        {
            return null;
        }

        var ip = http.Connection.RemoteIpAddress?.ToString();
        var ua = http.Request.Headers.UserAgent.ToString();
        var method = http.Request.Method;
        var path = http.Request.Path.Value;
        var traceId = http.TraceIdentifier;

        var md = new
        {
            ip,
            userAgent = ua,
            method,
            path,
            traceId,
            activityId = Activity.Current?.Id
        };

        return JsonSerializer.Serialize(md, JsonOpts);
    }

    private static (Guid? id, string? name) GetActor(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return (null, null);
        }

        // prefer sub/nameidentifier as Guid; fall back to Name
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? user.FindFirst("sub")?.Value;

        Guid? id = Guid.TryParse(sub, out var g) ? g : null;
        var name = user.Identity?.Name
                   ?? user.FindFirst(ClaimTypes.Email)?.Value
                   ?? user.FindFirst("preferred_username")?.Value;

        return (id, name);
    }
}
