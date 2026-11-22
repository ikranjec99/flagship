using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Flagship.Core.DataAccess;

public sealed class DatabaseMigrator(
    NpgsqlDataSource dataSource,
    ILogger<DatabaseMigrator> logger)
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            SqlScriptLoader.Load("Migrations/EnsureTable.sql"),
            cancellationToken: cancellationToken));

        var applied = (await connection.QueryAsync<int>(new CommandDefinition(
            SqlScriptLoader.Load("Migrations/GetApplied.sql"),
            cancellationToken: cancellationToken))).ToHashSet();

        var assembly = typeof(DatabaseMigrator).Assembly;
        var migrations = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".DataAccess.Migrations.", StringComparison.Ordinal)
                && name.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (var resourceName in migrations)
        {
            var fileName = resourceName.Split('.').Reverse().Skip(1).First();
            var version = int.Parse(fileName.AsSpan(0, 3));
            if (applied.Contains(version))
            {
                continue;
            }

            await using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Migration {resourceName} was not found.");
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync(cancellationToken);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(sql, transaction: transaction,
                cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(
                SqlScriptLoader.Load("Migrations/Record.sql"),
                new { Version = version }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Applied database migration {MigrationVersion}", version);
        }

        logger.LogInformation("Database schema is up to date");
    }
}
