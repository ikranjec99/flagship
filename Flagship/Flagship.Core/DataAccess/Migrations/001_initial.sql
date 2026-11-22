create table admin_users (
    id uuid primary key,
    email text not null unique,
    password_hash text not null,
    created_at timestamptz not null default now()
);

create table projects (
    id uuid primary key,
    name text not null,
    key text not null unique,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint projects_name_length check (char_length(name) between 1 and 100),
    constraint projects_key_format check (key ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$')
);

create table environments (
    id uuid primary key,
    project_id uuid not null references projects(id) on delete cascade,
    name text not null,
    key text not null,
    created_at timestamptz not null default now(),
    unique (project_id, key),
    constraint environments_name_length check (char_length(name) between 1 and 100),
    constraint environments_key_format check (key ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$')
);

create table feature_flags (
    id uuid primary key,
    project_id uuid not null references projects(id) on delete cascade,
    name text not null,
    key text not null,
    description text null,
    is_archived boolean not null default false,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique (project_id, key),
    constraint feature_flags_name_length check (char_length(name) between 1 and 100),
    constraint feature_flags_key_format check (key ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$')
);

create table flag_configurations (
    id uuid primary key,
    flag_id uuid not null references feature_flags(id) on delete cascade,
    environment_id uuid not null references environments(id) on delete cascade,
    enabled boolean not null default false,
    default_value boolean not null default false,
    rollout_percentage smallint null,
    version integer not null default 1,
    updated_at timestamptz not null default now(),
    unique (flag_id, environment_id),
    constraint rollout_percentage_range check (
        rollout_percentage is null or rollout_percentage between 0 and 100
    )
);

create table targeting_rules (
    id uuid primary key,
    configuration_id uuid not null references flag_configurations(id) on delete cascade,
    name text not null,
    position integer not null,
    serve_value boolean not null,
    unique (configuration_id, position),
    constraint targeting_rules_position check (position >= 0)
);

create table targeting_conditions (
    id uuid primary key,
    rule_id uuid not null references targeting_rules(id) on delete cascade,
    attribute text not null,
    operator text not null,
    comparison_value jsonb not null,
    position integer not null,
    unique (rule_id, position),
    constraint targeting_conditions_operator check (
        operator in ('equals', 'not_equals', 'one_of', 'not_one_of', 'greater_than', 'less_than')
    )
);

create table environment_api_keys (
    id uuid primary key,
    environment_id uuid not null references environments(id) on delete cascade,
    name text not null,
    prefix text not null,
    secret_hash bytea not null unique,
    created_at timestamptz not null default now(),
    revoked_at timestamptz null
);

create table audit_events (
    id bigint generated always as identity primary key,
    project_id uuid null references projects(id) on delete set null,
    environment_id uuid null references environments(id) on delete set null,
    actor text not null,
    action text not null,
    entity_type text not null,
    entity_id uuid null,
    summary text not null,
    before_value jsonb null,
    after_value jsonb null,
    note text null,
    occurred_at timestamptz not null default now()
);

create index ix_environments_project_id on environments(project_id);
create index ix_feature_flags_project_id on feature_flags(project_id);
create index ix_flag_configurations_environment_id on flag_configurations(environment_id);
create index ix_targeting_rules_configuration_id on targeting_rules(configuration_id);
create index ix_targeting_conditions_rule_id on targeting_conditions(rule_id);
create index ix_environment_api_keys_environment_id on environment_api_keys(environment_id);
create index ix_audit_events_project_occurred_at on audit_events(project_id, occurred_at desc);
