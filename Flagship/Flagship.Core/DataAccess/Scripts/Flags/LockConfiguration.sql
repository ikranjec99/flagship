select c.id, c.version, f.project_id, f.key as flag_key
from flag_configurations c
join feature_flags f on f.id = c.flag_id
where c.flag_id = @FlagId
  and c.environment_id = @EnvironmentId
for update;
