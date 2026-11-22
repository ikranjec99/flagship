update feature_flags
set is_archived = @Archived,
    updated_at = now()
where id = @FlagId
returning project_id, key;
