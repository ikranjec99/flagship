insert into flag_configurations (id, flag_id, environment_id)
select gen_random_uuid(), @FlagId, id
from environments
where project_id = @ProjectId;
