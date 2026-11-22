update environment_api_keys k
set revoked_at = now()
from environments e
where k.id = @KeyId
  and k.environment_id = @EnvironmentId
  and k.environment_id = e.id
  and k.revoked_at is null
returning e.project_id, k.name, k.prefix;
