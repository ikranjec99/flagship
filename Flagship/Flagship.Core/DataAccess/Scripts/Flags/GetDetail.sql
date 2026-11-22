select f.id, f.project_id, c.environment_id, f.name, f.key, f.description,
       f.is_archived, c.enabled, c.default_value, c.rollout_percentage,
       c.version, c.updated_at, c.id as configuration_id
from feature_flags f
join flag_configurations c on c.flag_id = f.id
where f.id = @FlagId
  and c.environment_id = @EnvironmentId;
