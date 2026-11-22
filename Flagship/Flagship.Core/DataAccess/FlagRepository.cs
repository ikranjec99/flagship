using System.Text.Json;
using Dapper;
using Flagship.Core.Business.Evaluation;
using Flagship.Core.Business.Exceptions;
using Flagship.Core.Business.Helpers;
using Flagship.Core.Business.Models;
using Npgsql;

namespace Flagship.Core.DataAccess;

public sealed class FlagRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<FlagListItemModel>> ListAsync(
        Guid projectId,
        Guid environmentId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var flags = await connection.QueryAsync<FlagListItemModel>(new CommandDefinition(
            SqlScriptLoader.Load("Flags/List.sql"),
            new
            {
                ProjectId = projectId,
                EnvironmentId = environmentId,
                IncludeArchived = includeArchived
            }, cancellationToken: cancellationToken));
        return flags.ToList();
    }

    public async Task<CreatedFlagModel> CreateAsync(
        Guid projectId,
        string name,
        string? requestedKey,
        string? description,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new RequestValidationException("flag", "Name is required.");
        }

        var key = Keys.Normalize(requestedKey ?? name);
        if (!Keys.IsValid(key))
        {
            throw new RequestValidationException("flag",
                "Key must contain lowercase letters, numbers, or hyphens.");
        }

        var flag = new CreatedFlagModel(
            Guid.NewGuid(), name.Trim(), key,
            string.IsNullOrWhiteSpace(description) ? null : description.Trim());
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var inserted = await connection.ExecuteAsync(new CommandDefinition(
                SqlScriptLoader.Load("Flags/Insert.sql"),
                new
                {
                    flag.Id,
                    ProjectId = projectId,
                    flag.Name,
                    flag.Key,
                    flag.Description
                }, transaction, cancellationToken: cancellationToken));
            if (inserted == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new EntityNotFoundException("Project was not found.");
            }

            await connection.ExecuteAsync(new CommandDefinition(
                SqlScriptLoader.Load("Flags/InsertConfigurations.sql"),
                new { FlagId = flag.Id, ProjectId = projectId }, transaction,
                cancellationToken: cancellationToken));
            await AuditWriter.WriteAsync(connection, transaction, projectId, null,
                actor, "created", "feature_flag", flag.Id,
                $"Created flag {key}", null, flag, null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return flag;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException("A flag with this key already exists in the project.");
        }
    }

    public async Task<FlagDetailModel> GetAsync(
        Guid flagId,
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await LoadDetailAsync(connection, flagId, environmentId, cancellationToken)
            ?? throw new EntityNotFoundException("Flag configuration was not found.");
    }

    public async Task<FlagDetailModel> UpdateConfigurationAsync(
        Guid flagId,
        Guid environmentId,
        FlagConfigurationInput input,
        string actor,
        CancellationToken cancellationToken)
    {
        Validate(input);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await connection.QuerySingleOrDefaultAsync<ConfigurationRow>(new CommandDefinition(
            SqlScriptLoader.Load("Flags/LockConfiguration.sql"),
            new { FlagId = flagId, EnvironmentId = environmentId }, transaction,
            cancellationToken: cancellationToken));
        if (current is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new EntityNotFoundException("Flag configuration was not found.");
        }

        if (current.Version != input.ExpectedVersion)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new VersionConflictException(current.Version);
        }

        var before = await LoadDetailAsync(connection, flagId, environmentId,
            cancellationToken, transaction);
        var updatedVersion = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            SqlScriptLoader.Load("Flags/UpdateConfiguration.sql"),
            new
            {
                input.Enabled,
                input.DefaultValue,
                input.RolloutPercentage,
                ConfigurationId = current.Id,
                input.ExpectedVersion
            }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            SqlScriptLoader.Load("Flags/DeleteRules.sql"),
            new { ConfigurationId = current.Id }, transaction,
            cancellationToken: cancellationToken));
        await InsertRulesAsync(connection, transaction, current.Id, input.Rules, cancellationToken);

        var after = input with { ExpectedVersion = updatedVersion };
        await AuditWriter.WriteAsync(connection, transaction, current.ProjectId, environmentId,
            actor, "configuration_updated", "feature_flag", flagId,
            $"Updated {current.FlagKey} configuration", before, after,
            input.ChangeNote?.Trim(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await LoadDetailAsync(connection, flagId, environmentId, cancellationToken)
            ?? throw new EntityNotFoundException("Flag configuration was not found after update.");
    }

    public async Task SetArchivedAsync(
        Guid flagId,
        bool archived,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var flag = await connection.QuerySingleOrDefaultAsync<ArchiveRow>(new CommandDefinition(
            SqlScriptLoader.Load("Flags/SetArchived.sql"),
            new { FlagId = flagId, Archived = archived }, transaction,
            cancellationToken: cancellationToken));
        if (flag is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new EntityNotFoundException("Flag was not found.");
        }

        await AuditWriter.WriteAsync(connection, transaction, flag.ProjectId, null,
            actor, archived ? "archived" : "restored", "feature_flag", flagId,
            $"{(archived ? "Archived" : "Restored")} flag {flag.Key}",
            new { IsArchived = !archived }, new { IsArchived = archived }, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<FlagDefinition?> GetDefinitionAsync(
        Guid projectId,
        Guid environmentId,
        string flagKey,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await LoadDefinitionAsync(
            connection, projectId, environmentId, flagKey, cancellationToken);
    }

    public async Task<IReadOnlyList<FlagDefinition>> GetDefinitionsAsync(
        Guid projectId,
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var keys = await connection.QueryAsync<string>(new CommandDefinition(
            SqlScriptLoader.Load("Flags/GetKeysForEnvironment.sql"),
            new { ProjectId = projectId, EnvironmentId = environmentId },
            cancellationToken: cancellationToken));
        var definitions = new List<FlagDefinition>();
        foreach (var key in keys)
        {
            var definition = await LoadDefinitionAsync(
                connection, projectId, environmentId, key, cancellationToken);
            if (definition is not null)
            {
                definitions.Add(definition);
            }
        }

        return definitions;
    }

    public async Task<Guid?> GetProjectIdForEnvironmentAsync(
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            SqlScriptLoader.Load("Flags/GetProjectByEnvironment.sql"),
            new { EnvironmentId = environmentId }, cancellationToken: cancellationToken));
    }

    private static async Task InsertRulesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid configurationId,
        IReadOnlyList<RuleInput> rules,
        CancellationToken cancellationToken)
    {
        for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
        {
            var rule = rules[ruleIndex];
            var ruleId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                SqlScriptLoader.Load("Flags/InsertRule.sql"),
                new
                {
                    Id = ruleId,
                    ConfigurationId = configurationId,
                    Name = rule.Name.Trim(),
                    Position = ruleIndex,
                    rule.ServeValue
                }, transaction, cancellationToken: cancellationToken));

            for (var conditionIndex = 0; conditionIndex < rule.Conditions.Count; conditionIndex++)
            {
                var condition = rule.Conditions[conditionIndex];
                await connection.ExecuteAsync(new CommandDefinition(
                    SqlScriptLoader.Load("Flags/InsertCondition.sql"),
                    new
                    {
                        Id = Guid.NewGuid(),
                        RuleId = ruleId,
                        Attribute = condition.Attribute.Trim(),
                        Operator = ConditionOperatorMapper.ToDatabaseValue(condition.Operator),
                        ComparisonValue = condition.ComparisonValue.GetRawText(),
                        Position = conditionIndex
                    }, transaction, cancellationToken: cancellationToken));
            }
        }
    }

    private static async Task<FlagDetailModel?> LoadDetailAsync(
        NpgsqlConnection connection,
        Guid flagId,
        Guid environmentId,
        CancellationToken cancellationToken,
        NpgsqlTransaction? transaction = null)
    {
        var row = await connection.QuerySingleOrDefaultAsync<FlagRow>(new CommandDefinition(
            SqlScriptLoader.Load("Flags/GetDetail.sql"),
            new { FlagId = flagId, EnvironmentId = environmentId }, transaction,
            cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        var rules = await LoadRulesAsync(
            connection, row.ConfigurationId, cancellationToken, transaction);
        return new FlagDetailModel(
            row.Id, row.ProjectId, row.EnvironmentId, row.Name, row.Key,
            row.Description, row.IsArchived, row.Enabled, row.DefaultValue,
            row.RolloutPercentage, row.Version, row.UpdatedAt, rules);
    }

    private static async Task<FlagDefinition?> LoadDefinitionAsync(
        NpgsqlConnection connection,
        Guid projectId,
        Guid environmentId,
        string flagKey,
        CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<FlagRow>(new CommandDefinition(
            SqlScriptLoader.Load("Flags/GetByKey.sql"),
            new { ProjectId = projectId, EnvironmentId = environmentId, FlagKey = flagKey },
            cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        var rules = await LoadDomainRulesAsync(connection, row.ConfigurationId, cancellationToken);
        return new FlagDefinition(row.Id, row.Key, row.Enabled, row.DefaultValue,
            row.RolloutPercentage, row.Version, rules);
    }

    private static async Task<IReadOnlyList<RuleModel>> LoadRulesAsync(
        NpgsqlConnection connection,
        Guid configurationId,
        CancellationToken cancellationToken,
        NpgsqlTransaction? transaction = null)
    {
        var ruleRows = (await connection.QueryAsync<RuleRow>(new CommandDefinition(
            SqlScriptLoader.Load("Flags/GetRules.sql"), new { ConfigurationId = configurationId },
            transaction, cancellationToken: cancellationToken))).ToList();
        var conditionRows = await LoadConditionRowsAsync(
            connection, configurationId, cancellationToken, transaction);
        return ruleRows.Select(rule => new RuleModel(
            rule.Id, rule.Name, rule.Position, rule.ServeValue,
            conditionRows.Where(condition => condition.RuleId == rule.Id)
                .Select(condition => new ConditionModel(
                    condition.Id, condition.Attribute,
                    ConditionOperatorMapper.FromDatabaseValue(condition.Operator),
                    JsonSerializer.Deserialize<JsonElement>(condition.ComparisonValueJson),
                    condition.Position)).ToList())).ToList();
    }

    private static async Task<IReadOnlyList<TargetingRule>> LoadDomainRulesAsync(
        NpgsqlConnection connection,
        Guid configurationId,
        CancellationToken cancellationToken)
    {
        var ruleRows = (await connection.QueryAsync<RuleRow>(new CommandDefinition(
            SqlScriptLoader.Load("Flags/GetRules.sql"), new { ConfigurationId = configurationId },
            cancellationToken: cancellationToken))).ToList();
        var conditionRows = await LoadConditionRowsAsync(connection, configurationId, cancellationToken);
        return ruleRows.Select(rule => new TargetingRule(
            rule.Id, rule.Name, rule.Position, rule.ServeValue,
            conditionRows.Where(condition => condition.RuleId == rule.Id)
                .Select(condition => new TargetingCondition(
                    condition.Attribute,
                    ConditionOperatorMapper.FromDatabaseValue(condition.Operator),
                    JsonSerializer.Deserialize<JsonElement>(condition.ComparisonValueJson)))
                .ToList())).ToList();
    }

    private static async Task<List<ConditionRow>> LoadConditionRowsAsync(
        NpgsqlConnection connection,
        Guid configurationId,
        CancellationToken cancellationToken,
        NpgsqlTransaction? transaction = null) =>
        (await connection.QueryAsync<ConditionRow>(new CommandDefinition(
            SqlScriptLoader.Load("Flags/GetConditions.sql"),
            new { ConfigurationId = configurationId }, transaction,
            cancellationToken: cancellationToken))).ToList();

    private static void Validate(FlagConfigurationInput input)
    {
        if (input.ExpectedVersion < 1)
        {
            throw new RequestValidationException("configuration",
                "Expected version must be positive.");
        }

        if (input.RolloutPercentage is < 0 or > 100)
        {
            throw new RequestValidationException("configuration",
                "Rollout percentage must be between 0 and 100.");
        }

        if (input.Rules is null
            || input.Rules.Any(rule => string.IsNullOrWhiteSpace(rule.Name)
                || rule.Conditions is null
                || rule.Conditions.Count == 0
                || rule.Conditions.Any(condition => string.IsNullOrWhiteSpace(condition.Attribute))))
        {
            throw new RequestValidationException("configuration",
                "Every rule needs a name and at least one valid condition.");
        }
    }

    private sealed record FlagRow(
        Guid Id,
        Guid ProjectId,
        Guid EnvironmentId,
        string Name,
        string Key,
        string? Description,
        bool IsArchived,
        bool Enabled,
        bool DefaultValue,
        int? RolloutPercentage,
        int Version,
        DateTimeOffset UpdatedAt,
        Guid ConfigurationId);
    private sealed record RuleRow(Guid Id, string Name, int Position, bool ServeValue);
    private sealed record ConditionRow(
        Guid Id,
        Guid RuleId,
        string Attribute,
        string Operator,
        string ComparisonValueJson,
        int Position);
    private sealed record ConfigurationRow(Guid Id, int Version, Guid ProjectId, string FlagKey);
    private sealed record ArchiveRow(Guid ProjectId, string Key);
}
