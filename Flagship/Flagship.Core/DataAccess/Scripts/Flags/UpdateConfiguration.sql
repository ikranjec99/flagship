update flag_configurations
set enabled = @Enabled,
    default_value = @DefaultValue,
    rollout_percentage = @RolloutPercentage,
    version = version + 1,
    updated_at = now()
where id = @ConfigurationId
  and version = @ExpectedVersion
returning version;
