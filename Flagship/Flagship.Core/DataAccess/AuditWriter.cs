using System.Text.Json;
using Dapper;
using Npgsql;

namespace Flagship.Core.DataAccess;

public static class AuditWriter
{
    public static Task WriteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid? projectId,
        Guid? environmentId,
        string actor,
        string action,
        string entityType,
        Guid? entityId,
        string summary,
        object? beforeValue,
        object? afterValue,
        string? note,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            SqlScriptLoader.Load("Audit/Insert.sql"), new
            {
                ProjectId = projectId,
                EnvironmentId = environmentId,
                Actor = actor,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Summary = summary,
                BeforeValue = beforeValue is null ? null : JsonSerializer.Serialize(beforeValue),
                AfterValue = afterValue is null ? null : JsonSerializer.Serialize(afterValue),
                Note = note
            }, transaction, cancellationToken: cancellationToken));
}
