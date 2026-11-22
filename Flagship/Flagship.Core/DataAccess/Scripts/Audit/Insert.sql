insert into audit_events (
    project_id, environment_id, actor, action, entity_type, entity_id,
    summary, before_value, after_value, note)
values (
    @ProjectId, @EnvironmentId, @Actor, @Action, @EntityType, @EntityId,
    @Summary, cast(@BeforeValue as jsonb), cast(@AfterValue as jsonb), @Note);
