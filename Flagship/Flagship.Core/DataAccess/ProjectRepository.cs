using Dapper;
using Flagship.Core.Business.Exceptions;
using Flagship.Core.Business.Helpers;
using Flagship.Core.Business.Models;
using Npgsql;

namespace Flagship.Core.DataAccess;

public sealed class ProjectRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<ProjectModel>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var projects = (await connection.QueryAsync<ProjectRow>(new CommandDefinition(
            SqlScriptLoader.Load("Projects/GetAll.sql"),
            cancellationToken: cancellationToken))).ToList();
        var environments = (await connection.QueryAsync<EnvironmentRow>(new CommandDefinition(
            SqlScriptLoader.Load("Projects/GetEnvironments.sql"),
            cancellationToken: cancellationToken))).ToList();

        return projects.Select(project => new ProjectModel(
            project.Id,
            project.Name,
            project.Key,
            project.CreatedAt,
            environments.Where(environment => environment.ProjectId == project.Id)
                .Select(environment => new EnvironmentModel(
                    environment.Id, environment.Name, environment.Key)).ToList())).ToList();
    }

    public async Task<ProjectModel> CreateAsync(
        string name,
        string? requestedKey,
        string actor,
        CancellationToken cancellationToken)
    {
        var key = ValidateNameAndKey(name, requestedKey, "project");
        var projectId = Guid.NewGuid();
        var environments = new[]
        {
            new EnvironmentModel(Guid.NewGuid(), "Development", "development"),
            new EnvironmentModel(Guid.NewGuid(), "Staging", "staging"),
            new EnvironmentModel(Guid.NewGuid(), "Production", "production")
        };

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                SqlScriptLoader.Load("Projects/InsertProject.sql"),
                new { Id = projectId, Name = name.Trim(), Key = key }, transaction,
                cancellationToken: cancellationToken));

            foreach (var environment in environments)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    SqlScriptLoader.Load("Projects/InsertEnvironment.sql"),
                    new { environment.Id, ProjectId = projectId, environment.Name, environment.Key },
                    transaction, cancellationToken: cancellationToken));
            }

            await AuditWriter.WriteAsync(connection, transaction, projectId, null,
                actor, "created", "project", projectId,
                $"Created project {name.Trim()}", null,
                new { Id = projectId, Name = name.Trim(), Key = key }, null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException("A project with this key already exists.");
        }

        return new ProjectModel(projectId, name.Trim(), key, DateTimeOffset.UtcNow, environments);
    }

    public async Task<EnvironmentModel> CreateEnvironmentAsync(
        Guid projectId,
        string name,
        string? requestedKey,
        string actor,
        CancellationToken cancellationToken)
    {
        var key = ValidateNameAndKey(name, requestedKey, "environment");
        var environment = new EnvironmentModel(Guid.NewGuid(), name.Trim(), key);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var inserted = await connection.ExecuteAsync(new CommandDefinition(
                SqlScriptLoader.Load("Projects/InsertEnvironment.sql"),
                new { environment.Id, ProjectId = projectId, environment.Name, environment.Key },
                transaction, cancellationToken: cancellationToken));
            if (inserted == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new EntityNotFoundException("Project was not found.");
            }

            await connection.ExecuteAsync(new CommandDefinition(
                SqlScriptLoader.Load("Projects/InsertEnvironmentConfigurations.sql"),
                new { ProjectId = projectId, EnvironmentId = environment.Id }, transaction,
                cancellationToken: cancellationToken));
            await AuditWriter.WriteAsync(connection, transaction, projectId, environment.Id,
                actor, "created", "environment", environment.Id,
                $"Created environment {environment.Name}", null, environment, null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return environment;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException("An environment with this key already exists.");
        }
    }

    private static string ValidateNameAndKey(string name, string? requestedKey, string field)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new RequestValidationException(field, "Name is required.");
        }

        var key = Keys.Normalize(requestedKey ?? name);
        if (!Keys.IsValid(key))
        {
            throw new RequestValidationException(field,
                "Key must contain lowercase letters, numbers, or hyphens.");
        }

        return key;
    }

    private sealed record ProjectRow(Guid Id, string Name, string Key, DateTimeOffset CreatedAt);
    private sealed record EnvironmentRow(Guid Id, Guid ProjectId, string Name, string Key);
}
