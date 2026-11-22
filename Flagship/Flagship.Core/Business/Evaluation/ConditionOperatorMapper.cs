namespace Flagship.Core.Business.Evaluation;

public static class ConditionOperatorMapper
{
    public static string ToDatabaseValue(ConditionOperator value) => value switch
    {
        ConditionOperator.Equals => "equals",
        ConditionOperator.NotEquals => "not_equals",
        ConditionOperator.OneOf => "one_of",
        ConditionOperator.NotOneOf => "not_one_of",
        ConditionOperator.GreaterThan => "greater_than",
        ConditionOperator.LessThan => "less_than",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static ConditionOperator FromDatabaseValue(string value) => value switch
    {
        "equals" => ConditionOperator.Equals,
        "not_equals" => ConditionOperator.NotEquals,
        "one_of" => ConditionOperator.OneOf,
        "not_one_of" => ConditionOperator.NotOneOf,
        "greater_than" => ConditionOperator.GreaterThan,
        "less_than" => ConditionOperator.LessThan,
        _ => throw new InvalidOperationException($"Unknown condition operator '{value}'.")
    };
}
