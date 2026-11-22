# Flagship

Flagship is a lightweight, self-hosted feature flag management platform for developers and small teams. It separates deployments from releases without requiring an external SaaS service or an operations stack beyond PostgreSQL.

> Flagship is under active MVP development. The evaluation contract and database migrations are not yet considered stable.

## What works

- Multiple projects and environments
- Boolean feature flags with immutable keys
- Per-environment serving configuration
- Ordered attribute-targeting rules
- Deterministic percentage rollouts
- Server API keys with one-time secret display and hashed storage
- Single-flag and all-flags evaluation endpoints
- Evaluation tester that explains each result
- Optimistic concurrency for configuration updates
- Append-only audit history
- Archive and restore lifecycle
- Docker Compose with bundled or external PostgreSQL

## Quick start with bundled PostgreSQL

Requirements: Docker Engine with Docker Compose. This option runs Flagship and PostgreSQL together and stores database data in a named Docker volume.

```bash
cp .env.example .env
# Edit .env and replace both placeholder passwords.
docker compose up -d --build
```

Open [http://localhost:8080](http://localhost:8080) and sign in with `FLAGSHIP_ADMIN_EMAIL` and `FLAGSHIP_ADMIN_PASSWORD` from `.env`.

Useful endpoints:

- Dashboard: [http://localhost:8080](http://localhost:8080)
- Swagger UI: [http://localhost:8080/swagger](http://localhost:8080/swagger)
- OpenAPI document: [http://localhost:8080/swagger/v1/swagger.json](http://localhost:8080/swagger/v1/swagger.json)
- Health check: [http://localhost:8080/health](http://localhost:8080/health)

Check startup or migration errors with:

```bash
docker compose ps
docker compose logs -f flagship
```

Stop the application with `docker compose down`. The `flagship-postgres` volume survives container recreation as long as `--volumes` is not supplied.

## Quick start with an existing PostgreSQL server

Create an empty PostgreSQL database and a role that owns it. The role must be able to create tables, indexes, constraints, and sequences. Flagship applies its versioned SQL migrations automatically on startup.

Copy `.env.example` to `.env`, set the administrator credentials, and uncomment/configure this value:

```dotenv
FLAGSHIP_DB_CONNECTION_STRING=Host=postgres.example.internal;Port=5432;Database=flagship;Username=flagship;Password=replace-me;SSL Mode=Require
```

Then start only the application container:

```bash
docker compose -f compose.external-db.yml up -d --build
```

For a PostgreSQL server on the Docker host, use `host.docker.internal` as the host name on Docker Desktop. On Linux, expose the database on a reachable interface or add an explicit host gateway mapping.

Open [http://localhost:8080](http://localhost:8080) after the container starts. The Swagger and health-check URLs are the same as in the bundled setup.

## Evaluate a flag

Create a project, flag, and server API key in the UI. Then call:

```bash
curl -X POST http://localhost:8080/api/evaluate/v1/flags/new-checkout \
  -H "Authorization: Bearer <server-api-key>" \
  -H "Content-Type: application/json" \
  -d '{"subjectKey":"user-123","attributes":{"plan":"pro","country":"HR"}}'
```

Example response:

```json
{
  "key": "new-checkout",
  "value": true,
  "reason": "PercentageRollout",
  "matchedRuleId": null,
  "rolloutBucket": 8.42,
  "configurationVersion": 3
}
```

Consumers must provide a local fallback and should cache successful results. An unavailable feature flag service must not make the consuming application unavailable.

## Local development

Requirements:

- .NET 10 SDK
- Node.js 24+
- PostgreSQL 16+, or Docker for the development database

First create `.env`, update its passwords, and start only PostgreSQL:

```bash
cp .env.example .env
docker compose -f compose.yml -f compose.dev.yml up -d postgres
```

The .NET configuration does not automatically read `.env` outside Docker. In a backend terminal, set values matching your `.env` file and start the API. PowerShell example:

```powershell
$env:ConnectionStrings__Flagship = "Host=localhost;Port=5432;Database=flagship;Username=flagship;Password=change-this-database-password"
$env:Flagship__Admin__Email = "admin@example.com"
$env:Flagship__Admin__Password = "change-this-to-a-long-random-password"
dotnet run --project Flagship/Flagship.API
```

Equivalent Bash configuration:

```bash
export ConnectionStrings__Flagship='Host=localhost;Port=5432;Database=flagship;Username=flagship;Password=change-this-database-password'
export Flagship__Admin__Email='admin@example.com'
export Flagship__Admin__Password='change-this-to-a-long-random-password'
dotnet run --project Flagship/Flagship.API
```

Use your own values in the commands above. The API starts at [http://localhost:5080](http://localhost:5080), Swagger UI is available at [http://localhost:5080/swagger](http://localhost:5080/swagger), and migrations run automatically.

In a second terminal, start the frontend:

```bash
cd frontend
npm install
npm run dev
```

Open [http://localhost:5173](http://localhost:5173). Vite proxies `/api` and `/health` to the backend on port `5080`.

For a production-style local build, run `npm run build` inside `frontend` before starting the API. The frontend is emitted into `Flagship/Flagship.API/wwwroot` and served by ASP.NET Core.

## API documentation

Swashbuckle generates the OpenAPI document from the controllers and hosts an interactive Swagger UI at `/swagger`. Authenticate through `POST /api/auth/login` in the UI before trying protected management endpoints; the browser retains the same-origin session cookie. Evaluation endpoints use a server API key in the `Authorization: Bearer <key>` header.

## Tests

```bash
dotnet test Flagship/Flagship.slnx
```

The current unit suite covers evaluation order, AND rule semantics, stable rollouts, missing subject keys, and rollout distribution.

## Logging

Flagship uses the built-in ASP.NET Core structured logger and writes to standard output by default. Logger calls deliberately exclude administrator emails, API-key values, subject keys, targeting attributes, request bodies, project and flag keys, request paths, and exception messages. Logs retain operational fields such as technical resource IDs, status codes, migration versions, evaluation reasons, and trace identifiers.

The product's append-only audit history is separate from application logging and records the authenticated administrator as the actor. Treat the database as application data and define appropriate access, backup, and retention policies for your deployment. Configure application log levels with the standard `Logging__LogLevel__*` environment variables.

## Repository structure

```text
frontend/                         React, TypeScript, Vite, Zustand
Flagship/
  Flagship.API/                   ASP.NET Core host and HTTP endpoints
    Configuration/                API startup and bootstrap concerns
    Controllers/                  Thin HTTP controllers
    Contracts/                    API request contracts
    Filters/                      Centralized API exception handling
  Flagship.Core/                  Business logic and persistence support
    Business/Evaluation/          Pure evaluation engine
    Business/Helpers/             Shared domain helpers
    DataAccess/                   Dapper repositories and SQL scripts
  Flagship.Tests/                 xUnit domain tests
  Flagship.slnx                   Backend solution
```

The backend is a modular monolith. Controllers handle HTTP concerns, while focused Core repositories own Dapper operations and load embedded `.sql` scripts from `Flagship.Core/DataAccess/Scripts`. It deliberately avoids generic repositories, a mediator, and separate microservices.

## Security notes

- Use HTTPS at the reverse proxy before exposing Flagship outside a trusted network.
- Do not embed server API keys in browser applications.
- API key secrets are shown once; only SHA-256 hashes are stored.
- The initial administrator is created only when the database contains no administrators.
- Back up the PostgreSQL database and test restoration before depending on Flagship in production.

## License

MIT
