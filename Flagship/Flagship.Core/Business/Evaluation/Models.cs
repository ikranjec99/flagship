using System.Text.Json;

namespace Flagship.Core.Business.Evaluation;

public enum ConditionOperator
{
    Equals,
    NotEquals,
    OneOf,
    NotOneOf,
    GreaterThan,
    LessThan
}

public sealed record EvaluationSubject(
    string? SubjectKey,
    IReadOnlyDictionary<string, JsonElement> Attributes);

public sealed record FlagDefinition(
    Guid Id,
    string Key,
    bool Enabled,
    bool DefaultValue,
    int? RolloutPercentage,
    int Version,
    IReadOnlyList<TargetingRule> Rules);

public sealed record TargetingRule(
    Guid Id,
    string Name,
    int Position,
    bool ServeValue,
    IReadOnlyList<TargetingCondition> Conditions);

public sealed record TargetingCondition(
    string Attribute,
    ConditionOperator Operator,
    JsonElement ComparisonValue);

public sealed record EvaluationResult(
    string Key,
    bool Value,
    string Reason,
    Guid? MatchedRuleId,
    decimal? RolloutBucket,
    int ConfigurationVersion);
