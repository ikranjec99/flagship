select c.id, c.rule_id, c.attribute, c.operator,
       c.comparison_value::text as comparison_value_json, c.position
from targeting_conditions c
join targeting_rules r on r.id = c.rule_id
where r.configuration_id = @ConfigurationId
order by c.position;
