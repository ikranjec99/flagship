select k.environment_id, e.project_id, e.key as environment_key
from environment_api_keys k
join environments e on e.id = k.environment_id
where k.secret_hash = @Hash
  and k.revoked_at is null;
