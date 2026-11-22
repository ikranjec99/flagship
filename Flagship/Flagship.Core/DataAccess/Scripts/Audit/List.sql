select id, project_id, environment_id, actor, action, entity_type, entity_id,
       summary, note, occurred_at
from audit_events
where project_id = @ProjectId
  and (@EnvironmentId is null or environment_id = @EnvironmentId)
order by occurred_at desc
limit 100;
