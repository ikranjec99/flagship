using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Flagship.Core.Business.Evaluation;

public sealed class FlagEvaluator
{
    public EvaluationResult Evaluate(FlagDefinition flag, EvaluationSubject subject)
    {
        if (!flag.Enabled)
        {
            return Result(flag, false, "FlagDisabled");
        }

        foreach (var rule in flag.Rules.OrderBy(rule => rule.Position))
        {
            if (rule.Conditions.Count > 0
                && rule.Conditions.All(condition => Matches(condition, subject)))
            {
                return new EvaluationResult(flag.Key, rule.ServeValue, "RuleMatch", rule.Id,
                    null, flag.Version);
            }
        }

        if (flag.RolloutPercentage is { } percentage)
        {
            if (string.IsNullOrWhiteSpace(subject.SubjectKey))
            {
                return Result(flag, flag.DefaultValue, "MissingSubjectKey");
            }

            var bucket = GetBucket(flag.Id, subject.SubjectKey);
            return new EvaluationResult(flag.Key, bucket < percentage, "PercentageRollout",
                null, bucket, flag.Version);
        }

        return Result(flag, flag.DefaultValue, "DefaultValue");
    }

    public static decimal GetBucket(Guid flagId, string subjectKey)
    {
        var input = Encoding.UTF8.GetBytes($"{flagId:N}:{subjectKey}");
        var hash = SHA256.HashData(input);
        var value = BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(0, 4));
        return value % 10_000 / 100m;
    }

    private static bool Matches(TargetingCondition condition, EvaluationSubject subject)
    {
        JsonElement actual;
        if (string.Equals(condition.Attribute, "subjectKey", StringComparison.Ordinal))
        {
            if (subject.SubjectKey is null)
            {
                return false;
            }

            actual = JsonSerializer.SerializeToElement(subject.SubjectKey);
        }
        else if (!subject.Attributes.TryGetValue(condition.Attribute, out actual))
        {
            return false;
        }

        return condition.Operator switch
        {
            ConditionOperator.Equals => Equal(actual, condition.ComparisonValue),
            ConditionOperator.NotEquals => !Equal(actual, condition.ComparisonValue),
            ConditionOperator.OneOf => Contains(condition.ComparisonValue, actual),
            ConditionOperator.NotOneOf => !Contains(condition.ComparisonValue, actual),
            ConditionOperator.GreaterThan => CompareNumbers(actual, condition.ComparisonValue) > 0,
            ConditionOperator.LessThan => CompareNumbers(actual, condition.ComparisonValue) < 0,
            _ => false
        };
    }

    private static bool Equal(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        return left.ValueKind switch
        {
            JsonValueKind.String => string.Equals(left.GetString(), right.GetString(),
                StringComparison.Ordinal),
            JsonValueKind.Number => left.TryGetDecimal(out var leftNumber)
                && right.TryGetDecimal(out var rightNumber) && leftNumber == rightNumber,
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            JsonValueKind.Null => true,
            _ => left.GetRawText() == right.GetRawText()
        };
    }

    private static bool Contains(JsonElement collection, JsonElement value) =>
        collection.ValueKind == JsonValueKind.Array
        && collection.EnumerateArray().Any(candidate => Equal(candidate, value));

    private static int CompareNumbers(JsonElement left, JsonElement right)
    {
        if (!decimal.TryParse(left.GetRawText(), NumberStyles.Number, CultureInfo.InvariantCulture,
                out var leftNumber)
            || !decimal.TryParse(right.GetRawText(), NumberStyles.Number, CultureInfo.InvariantCulture,
                out var rightNumber))
        {
            return 0;
        }

        return leftNumber.CompareTo(rightNumber);
    }

    private static EvaluationResult Result(FlagDefinition flag, bool value, string reason) =>
        new(flag.Key, value, reason, null, null, flag.Version);
}
