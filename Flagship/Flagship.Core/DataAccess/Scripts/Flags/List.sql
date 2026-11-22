select f.id, f.name, f.key, f.description, f.is_archived,
       c.enabled, c.default_value, c.rollout_percentage, c.version, c.updated_at,
       count(r.id)::integer as rule_count
from feature_flags f
join flag_configurations c on c.flag_id = f.id
left join targeting_rules r on r.configuration_id = c.id
where f.project_id = @ProjectId
  and c.environment_id = @EnvironmentId
  and (@IncludeArchived or not f.is_archived)
group by f.id, c.id
order by f.name;
