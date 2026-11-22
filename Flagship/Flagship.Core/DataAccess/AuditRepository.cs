using Dapper;
using Flagship.Core.Business.Models;
using Npgsql;

namespace Flagship.Core.DataAccess;

public sealed class AuditRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<AuditEventModel>> ListAsync(
        Guid projectId,
        Guid? environmentId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var events = await connection.QueryAsync<AuditEventModel>(new CommandDefinition(
            SqlScriptLoader.Load("Audit/List.sql"),
            new { ProjectId = projectId, EnvironmentId = environmentId },
            cancellationToken: cancellationToken));
        return events.ToList();
    }
}
