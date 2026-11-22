using System.Text.Json;
using Flagship.Core.Business.Evaluation;
using Xunit;

namespace Flagship.Tests;

public sealed class FlagEvaluatorTests
{
    private readonly FlagEvaluator _evaluator = new();

    [Fact]
    public void Disabled_flag_always_returns_false()
    {
        var flag = Flag(enabled: false, defaultValue: true, rolloutPercentage: 100);

        var result = _evaluator.Evaluate(flag, Subject("user-1"));

        Assert.False(result.Value);
        Assert.Equal("FlagDisabled", result.Reason);
    }

    [Fact]
    public void First_matching_rule_wins()
    {
        var firstRuleId = Guid.NewGuid();
        var flag = Flag(rules:
        [
            Rule(firstRuleId, 0, false, Condition("plan", ConditionOperator.Equals, "\"pro\"")),
            Rule(Guid.NewGuid(), 1, true, Condition("country", ConditionOperator.Equals, "\"HR\""))
        ]);

        var result = _evaluator.Evaluate(flag, Subject("user-1", ("plan", "\"pro\""), ("country", "\"HR\"")));

        Assert.False(result.Value);
        Assert.Equal("RuleMatch", result.Reason);
        Assert.Equal(firstRuleId, result.MatchedRuleId);
    }

    [Fact]
    public void All_conditions_in_a_rule_must_match()
    {
        var flag = Flag(defaultValue: false, rules:
        [
            Rule(Guid.NewGuid(), 0, true,
                Condition("plan", ConditionOperator.Equals, "\"pro\""),
                Condition("country", ConditionOperator.OneOf, "[\"HR\",\"DE\"]"))
        ]);

        var result = _evaluator.Evaluate(flag, Subject("user-1", ("plan", "\"pro\""), ("country", "\"US\"")));

        Assert.False(result.Value);
        Assert.Equal("DefaultValue", result.Reason);
    }

    [Fact]
    public void Rollout_is_stable_for_the_same_subject()
    {
        var flag = Flag(rolloutPercentage: 25);

        var first = _evaluator.Evaluate(flag, Subject("stable-user"));
        var second = _evaluator.Evaluate(flag, Subject("stable-user"));

        Assert.Equal(first.Value, second.Value);
        Assert.Equal(first.RolloutBucket, second.RolloutBucket);
        Assert.Equal("PercentageRollout", first.Reason);
    }

    [Fact]
    public void Rollout_without_subject_key_uses_default_value()
    {
        var flag = Flag(defaultValue: true, rolloutPercentage: 25);

        var result = _evaluator.Evaluate(flag, Subject(null));

        Assert.True(result.Value);
        Assert.Equal("MissingSubjectKey", result.Reason);
    }

    [Fact]
    public void Rollout_distribution_is_close_to_configured_percentage()
    {
        var flag = Flag(rolloutPercentage: 25);

        var enabled = Enumerable.Range(0, 10_000)
            .Count(index => _evaluator.Evaluate(flag, Subject($"user-{index}")).Value);

        Assert.InRange(enabled, 2_300, 2_700);
    }

    private static FlagDefinition Flag(
        bool enabled = true,
        bool defaultValue = false,
        int? rolloutPercentage = null,
        IReadOnlyList<TargetingRule>? rules = null) =>
        new(Guid.Parse("4c6829d9-a066-4f50-a224-6b8be4cc9f33"), "test-flag", enabled,
            defaultValue, rolloutPercentage, 3, rules ?? []);

    private static TargetingRule Rule(
        Guid id,
        int position,
        bool serveValue,
        params TargetingCondition[] conditions) =>
        new(id, $"Rule {position}", position, serveValue, conditions);

    private static TargetingCondition Condition(
        string attribute,
        ConditionOperator conditionOperator,
        string comparisonJson) =>
        new(attribute, conditionOperator, Json(comparisonJson));

    private static EvaluationSubject Subject(
        string? key,
        params (string Name, string Json)[] attributes) =>
        new(key, attributes.ToDictionary(item => item.Name, item => Json(item.Json)));

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
