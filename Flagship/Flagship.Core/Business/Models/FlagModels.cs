using System.Text.Json;
using Flagship.Core.Business.Evaluation;

namespace Flagship.Core.Business.Models;

public sealed record CreatedFlagModel(
    Guid Id,
    string Name,
    string Key,
    string? Description);

public sealed record FlagListItemModel(
    Guid Id,
    string Name,
    string Key,
    string? Description,
    bool IsArchived,
    bool Enabled,
    bool DefaultValue,
    int? RolloutPercentage,
    int RuleCount,
    int Version,
    DateTimeOffset UpdatedAt);

public sealed record ConditionModel(
    Guid Id,
    string Attribute,
    ConditionOperator Operator,
    JsonElement ComparisonValue,
    int Position);

public sealed record RuleModel(
    Guid Id,
    string Name,
    int Position,
    bool ServeValue,
    IReadOnlyList<ConditionModel> Conditions);

public sealed record FlagDetailModel(
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
    IReadOnlyList<RuleModel> Rules);

public sealed record ConditionInput(
    string Attribute,
    ConditionOperator Operator,
    JsonElement ComparisonValue);

public sealed record RuleInput(
    string Name,
    bool ServeValue,
    IReadOnlyList<ConditionInput> Conditions);

public sealed record FlagConfigurationInput(
    bool Enabled,
    bool DefaultValue,
    int? RolloutPercentage,
    int ExpectedVersion,
    string? ChangeNote,
    IReadOnlyList<RuleInput> Rules);
