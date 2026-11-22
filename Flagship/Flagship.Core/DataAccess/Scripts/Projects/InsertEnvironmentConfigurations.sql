insert into flag_configurations (id, flag_id, environment_id)
select gen_random_uuid(), id, @EnvironmentId
from feature_flags
where project_id = @ProjectId;
