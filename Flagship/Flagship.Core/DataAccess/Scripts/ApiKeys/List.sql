select id, name, prefix, created_at, revoked_at
from environment_api_keys
where environment_id = @EnvironmentId
order by created_at desc;
