create table if not exists schema_migrations (
    version integer primary key,
    applied_at timestamptz not null default now()
);
