select f.key
from feature_flags f
join flag_configurations c on c.flag_id = f.id
where f.project_id = @ProjectId
  and c.environment_id = @EnvironmentId
  and not f.is_archived
order by f.key;
