using Dapper;
using Flagship.Core.Business.Models;
using Npgsql;

namespace Flagship.Core.DataAccess;

public sealed class AdminRepository(NpgsqlDataSource dataSource)
{
    public async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            SqlScriptLoader.Load("Admins/Count.sql"), cancellationToken: cancellationToken));
    }

    public async Task<AdminUserModel?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AdminUserModel>(new CommandDefinition(
            SqlScriptLoader.Load("Admins/FindByEmail.sql"),
            new { Email = email.Trim().ToLowerInvariant() },
            cancellationToken: cancellationToken));
    }

    public async Task InsertAsync(
        Guid id,
        string email,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            SqlScriptLoader.Load("Admins/Insert.sql"),
            new { Id = id, Email = email, PasswordHash = passwordHash },
            cancellationToken: cancellationToken));
    }
}
