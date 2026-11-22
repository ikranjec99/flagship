using System.Security.Cryptography;
using System.Text;
using Dapper;
using Flagship.Core.Business.Exceptions;
using Flagship.Core.Business.Models;
using Npgsql;

namespace Flagship.Core.DataAccess;

public sealed class ApiKeyRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<ApiKeyModel>> ListAsync(
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var keys = await connection.QueryAsync<ApiKeyModel>(new CommandDefinition(
            SqlScriptLoader.Load("ApiKeys/List.sql"), new { EnvironmentId = environmentId },
            cancellationToken: cancellationToken));
        return keys.ToList();
    }

    public async Task<CreatedApiKeyModel> CreateAsync(
        Guid environmentId,
        string name,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            throw new RequestValidationException("name",
                "A key name between 1 and 100 characters is required.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var environment = await connection.QuerySingleOrDefaultAsync<EnvironmentRow>(new CommandDefinition(
            SqlScriptLoader.Load("ApiKeys/GetEnvironment.sql"),
            new { EnvironmentId = environmentId }, transaction,
            cancellationToken: cancellationToken));
        if (environment is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new EntityNotFoundException("Environment was not found.");
        }

        var generated = Generate(environment.Key);
        var keyId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        await connection.ExecuteAsync(new CommandDefinition(
            SqlScriptLoader.Load("ApiKeys/Insert.sql"),
            new
            {
                Id = keyId,
                EnvironmentId = environmentId,
                Name = name.Trim(),
                generated.Prefix,
                generated.Hash,
                CreatedAt = createdAt
            }, transaction, cancellationToken: cancellationToken));

        await AuditWriter.WriteAsync(connection, transaction, environment.ProjectId, environmentId,
            actor, "created", "api_key", keyId,
            $"Created API key {name.Trim()}", null,
            new { Id = keyId, Name = name.Trim(), generated.Prefix }, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CreatedApiKeyModel(
            keyId, name.Trim(), generated.Prefix, generated.Secret, createdAt);
    }

    public async Task RevokeAsync(
        Guid environmentId,
        Guid keyId,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var key = await connection.QuerySingleOrDefaultAsync<RevokedKeyRow>(new CommandDefinition(
            SqlScriptLoader.Load("ApiKeys/Revoke.sql"),
            new { KeyId = keyId, EnvironmentId = environmentId }, transaction,
            cancellationToken: cancellationToken));
        if (key is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new EntityNotFoundException("API key was not found or is already revoked.");
        }

        await AuditWriter.WriteAsync(connection, transaction, key.ProjectId, environmentId,
            actor, "revoked", "api_key", keyId,
            $"Revoked API key {key.Name}", new { key.Prefix, Revoked = false },
            new { key.Prefix, Revoked = true }, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ApiKeyIdentity?> AuthenticateAsync(
        string secret,
        CancellationToken cancellationToken)
    {
        if (secret.Length < 30)
        {
            return null;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ApiKeyIdentity>(new CommandDefinition(
            SqlScriptLoader.Load("ApiKeys/Authenticate.sql"), new { Hash = hash },
            cancellationToken: cancellationToken));
    }

    private static (string Secret, string Prefix, byte[] Hash) Generate(string environmentKey)
    {
        var random = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var secret = $"flg_{environmentKey}_{random}";
        var prefix = secret[..Math.Min(secret.Length, 20)];
        return (secret, prefix, SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }

    private sealed record EnvironmentRow(Guid ProjectId, string Key);
    private sealed record RevokedKeyRow(Guid ProjectId, string Name, string Prefix);
}
