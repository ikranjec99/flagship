insert into targeting_conditions (
    id, rule_id, attribute, operator, comparison_value, position)
values (
    @Id, @RuleId, @Attribute, @Operator,
    cast(@ComparisonValue as jsonb), @Position);
