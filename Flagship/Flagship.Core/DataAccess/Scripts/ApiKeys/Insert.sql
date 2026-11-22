insert into environment_api_keys (
    id, environment_id, name, prefix, secret_hash, created_at)
values (@Id, @EnvironmentId, @Name, @Prefix, @Hash, @CreatedAt);
