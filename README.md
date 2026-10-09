# Helpdesk

A support ticket system built as a public portfolio project: a layered ASP.NET Core backend and a React frontend.

**Status:** Sprint 3 in progress. The backend API covers authentication, tickets, assignment, status changes, comments and audit history, and the whole stack (PostgreSQL, migrations, API and frontend) runs with Docker Compose. Still to come in Sprint 3: CI, the frontend UI and deployment.

## Tech stack

| Area | Technologies |
|---|---|
| Backend | ASP.NET Core on .NET 10, C#, EF Core, FluentValidation |
| Authentication | JWT bearer access tokens, rotating refresh tokens, role-based policies |
| Database | PostgreSQL 18 |
| Tests | xUnit, Testcontainers, WebApplicationFactory |
| Frontend | React, TypeScript, Vite, Tailwind CSS |
| Tooling | pnpm, oxlint |
| Containers | Docker multi-stage images, Docker Compose, nginx |

## Architecture

The backend follows a layered architecture. Dependencies point inward, so business rules never depend on the web framework or the database.

| Project | Responsibility | References |
|---|---|---|
| `Helpdesk.Domain` | Entities, enums and business rules | none |
| `Helpdesk.Application` | Use cases, DTOs, validators and repository interfaces | Domain |
| `Helpdesk.Infrastructure` | Implementations of the Application interfaces (persistence, password hashing, tokens) | Application, Domain |
| `Helpdesk.Api` | HTTP endpoints, authentication, error handling and composition root | Application, Infrastructure |
| `Helpdesk.Tests` | Unit and integration tests | Application, Api |

`Helpdesk.Domain` has no project or package references.

## Repository structure

```
.
├── backend/
│   ├── Helpdesk.sln
│   ├── Directory.Build.props   # shared settings: net10.0, nullable, warnings as errors
│   ├── Dockerfile              # API image (target runtime) and migrations runner (target migrator)
│   ├── Dockerfile.dockerignore # build context allow-list for backend/Dockerfile
│   ├── src/
│   │   ├── Helpdesk.Api/
│   │   ├── Helpdesk.Application/
│   │   ├── Helpdesk.Domain/
│   │   └── Helpdesk.Infrastructure/
│   └── tests/
│       └── Helpdesk.Tests/
├── frontend/                   # Vite + React + TypeScript + Tailwind CSS
│   ├── Dockerfile              # builds the app and serves it with unprivileged nginx
│   ├── .dockerignore           # build context allow-list
│   └── nginx.conf              # static files, SPA fallback and /api/ proxy to the API
├── docs/                       # project specification and plan
├── .github/workflows/          # CI workflows (empty for now)
├── .env.example
├── docker-compose.yml          # PostgreSQL, migrations, API and frontend
├── dotnet-tools.json           # local .NET tools (dotnet-ef)
└── global.json                 # pins the .NET SDK to 10.x
```

## Getting started

### Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) 22.12 or later
- [pnpm](https://pnpm.io/installation) (the version is pinned in `frontend/package.json`)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (full stack, local database and integration tests)

### Environment variables

Copy the example file and fill in your local values:

```bash
cp .env.example .env
```

`.env` is ignored by Git. `docker compose` reads it; the API run with `dotnet run` does not (it uses user secrets, see below).

| Variable | Required | Used for |
|---|---|---|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | Always: compose refuses to start without them | Database name and credentials; compose also builds the API connection string from them |
| `POSTGRES_PORT` | No (default `5432`) | Host port of the database, bound to `127.0.0.1` |
| `Jwt__SigningKey` | Full stack only, not for `docker compose up -d db` | JWT signing key: at least 32 random bytes encoded as base64 (see [JWT signing key](#jwt-signing-key)) |
| `API_PORT` | No (default `8080`) | Host port of the API, bound to `127.0.0.1` |
| `WEB_PORT` | No (default `8081`) | Host port of the frontend, bound to `127.0.0.1` |

### Run the full stack with Docker

Requires Docker Desktop. Copy `.env.example` to `.env`, fill in the `POSTGRES_*` values and generate a `Jwt__SigningKey`:

```bash
openssl rand -base64 48
```

Or in PowerShell 5.1 or 7:

```powershell
$bytes = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes); [Convert]::ToBase64String($bytes)
```

Then, from the repository root:

```bash
docker compose up --build
```

| Service | URL | Notes |
|---|---|---|
| `web` | http://127.0.0.1:8081 | The frontend. It proxies `/api/` to the API, so the browser talks to a single origin |
| `api` | http://127.0.0.1:8080 | The API, running in `Production`: it does not expose the OpenAPI document. Its container healthcheck calls [`/health`](#operations) |
| `db` | `127.0.0.1:5432` | PostgreSQL 18, with data in the `pgdata` volume |
| `migrate` | none | Applies the migrations and exits |

The ports come from `WEB_PORT`, `API_PORT` and `POSTGRES_PORT`. Compose starts the services in order: `migrate` waits until the database is healthy, the API waits until `migrate` finishes successfully, and the frontend starts once the API is healthy.

**Migrations.** The `migrate` service runs an EF Core migrations bundle that applies the migrations committed in the repository and exits. The API still never migrates the database on startup. The bundle is idempotent: on every later `docker compose up` it applies only the pending migrations, usually none.

Stop the stack with:

```bash
docker compose down
```

> **Warning:** `docker compose down -v` also deletes the `pgdata` volume, which holds the database, including the one used for local development. Use it only when you want to start from an empty database.

After changing code, rebuild the images with `docker compose up --build`.

Known limitations:

- Secrets reach the containers as environment variables, so they are visible with `docker inspect`.
- A `;` in `POSTGRES_PASSWORD` breaks the connection string that compose builds from it.
- The health check has its own limitations; see [Operations](#operations).
- A missing `Jwt__SigningKey` is detected by the application, not by compose: `migrate` fails with `'Jwt:SigningKey' is not configured` and the API does not start.
- `migrate` receives the JWT signing key only to pass the startup validation it shares with the API; it does not use it.
- HTTPS redirection has no effect in the container, which serves plain HTTP; TLS arrives with the deployment (HU-14).
- GSS encryption is disabled in the connection string (`GSS Encryption Mode=Disable`) because the image has no Kerberos library.

### Local database

To run the API with `dotnet run` and the frontend with `pnpm dev`, start only the database, from the repository root:

```bash
docker compose up -d db              # PostgreSQL 18 on 127.0.0.1:${POSTGRES_PORT}; Jwt__SigningKey not needed
dotnet tool restore                  # installs dotnet-ef from dotnet-tools.json
```

`docker compose up -d` without `db` starts the whole stack instead.

The API reads the connection string from `ConnectionStrings:DefaultConnection`. For local development store it with user secrets, outside the repository:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=helpdesk;Username=<user>;Password=<password>" \
  --project backend/src/Helpdesk.Api
```

Use the same port, user and password as in `.env`. In containers and hosting, set the `ConnectionStrings__DefaultConnection` environment variable instead.

### JWT signing key

The API signs access tokens with a secret key of at least 32 random bytes, encoded as base64, and refuses to start without it. Generate one and store it with user secrets.

PowerShell (works in Windows PowerShell 5.1 and PowerShell 7):

```powershell
$bytes = New-Object byte[] 48
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String($bytes)) --project backend/src/Helpdesk.Api
```

Bash, with OpenSSL:

```bash
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project backend/src/Helpdesk.Api
```

In containers and hosting, set the `Jwt__SigningKey` environment variable instead. Issuer, audience and token lifetimes are not secret and live in `appsettings.json` under `Jwt`.

Apply the migrations:

```bash
dotnet ef database update --project backend/src/Helpdesk.Infrastructure --startup-project backend/src/Helpdesk.Api
```

To add a migration after changing the model:

```bash
dotnet ef migrations add <Name> --project backend/src/Helpdesk.Infrastructure --startup-project backend/src/Helpdesk.Api --output-dir Persistence/Migrations
```

Migrations are applied explicitly; the API does not migrate the database on startup. In the full Docker stack the `migrate` service applies them (see [Run the full stack with Docker](#run-the-full-stack-with-docker)).

The migrations seed four categories: `General`, `Technical issue`, `Billing` and `Account`. Categories are listed in name order with PostgreSQL's ICU collation `und-x-icu`, so the server must be built with ICU support; the official PostgreSQL 18 image is, both as `postgres:18.6-trixie` in `docker-compose.yml` and as `postgres:18` in the tests.

### Staff users

Public sign-up only creates clients, and there is no endpoint yet to create agents or admins ([#19](https://github.com/yorkael/Helpdesk/issues/19)). To try assignment, status changes or the audit history locally, register a user through the API and change its role in the database, with the `POSTGRES_USER` and `POSTGRES_DB` values from `.env`:

```bash
docker compose exec db psql -U <POSTGRES_USER> -d <POSTGRES_DB> -c "UPDATE users SET role = 'Agent' WHERE email = '<email>';"
```

The command works the same whether you started only the database or the full stack. Use `Admin` instead of `Agent` for an admin. Log in again after the change: the role travels in the access token, so tokens issued before it keep the old role.

### Backend

From the repository root:

```bash
dotnet build backend/Helpdesk.sln
dotnet test backend/Helpdesk.sln
dotnet run --project backend/src/Helpdesk.Api
```

`dotnet test` requires Docker to be running: the persistence and API tests start a disposable PostgreSQL 18 container with Testcontainers. The API tests host the application in memory with their own settings and a random signing key, so they do not need user secrets.

The unit tests (Application and Domain) never touch the database (the Application tests use in-memory fakes), so they do not need Docker. To run only them:

```bash
dotnet test backend/Helpdesk.sln --filter "FullyQualifiedName~Helpdesk.Tests.Application|FullyQualifiedName~Helpdesk.Tests.Domain"
```

A guard test fails if a test in those namespaces uses a database fixture, so the filter stays free of Docker.

With `dotnet run`, the API listens on http://localhost:5038 and fails at startup if the connection string or the JWT settings are missing or invalid. In Development the OpenAPI document is served at http://localhost:5038/openapi/v1.json.

### Frontend

```bash
cd frontend
pnpm install
pnpm dev
```

The app runs on http://localhost:5173.

## API

| Method | Route | Access | Result |
|---|---|---|---|
| `POST` | `/api/auth/register` | anonymous | `201` with the new user; public sign-up always creates a `Client` |
| `POST` | `/api/auth/login` | anonymous | `200` with an access token and a refresh token |
| `POST` | `/api/auth/refresh` | anonymous | `200` with a new token pair; the refresh token used is revoked |
| `POST` | `/api/auth/logout` | anonymous | `204`, also for unknown or already revoked tokens |
| `GET` | `/api/auth/me` | authenticated | `200` with the caller as described by the access token |
| `POST` | `/api/tickets` | `ClientOnly` | `201` with the new ticket, created as `Open` |
| `GET` | `/api/tickets` | authenticated | `200` with a page of the tickets the caller may see |
| `PUT` | `/api/tickets/{id}/assignee` | `StaffOnly` | `200` with the ticket assigned to an active agent |
| `PUT` | `/api/tickets/{id}/status` | `StaffOnly` | `200` with the ticket in its new status |
| `POST` | `/api/tickets/{id}/comments` | authenticated | `201` with the new comment |
| `GET` | `/api/tickets/{id}/comments` | authenticated | `200` with the ticket's comments, oldest first |
| `GET` | `/api/tickets/{id}/history` | `AdminOnly` | `200` with the ticket's audit history, oldest first |
| `GET` | `/api/categories` | authenticated | `200` with every category, ordered by name |

### Tickets

- **Visibility** depends on the caller's role, read from the access token: admins see every ticket, agents see tickets assigned to them or unassigned, and clients see the tickets they created. A ticket outside the caller's scope answers `404`, like one that does not exist.
- **Listing** accepts `page`, `pageSize` (default 20, maximum 100), `status`, `priority`, `assignedToId`, `categoryId` and `search`, a case-insensitive match on the title or description. Results are newest first, and the response includes `totalCount`.
- **Priorities** are `Low`, `Medium`, `High` and `Urgent`. The creator comes from the access token; a client cannot set the assignee or the status.
- **Assignment:** an agent can only assign a ticket to themselves; an admin can assign or reassign any ticket. A closed ticket cannot be assigned.
- **Status changes** follow this table; any other move answers `409`, and sending the current status changes nothing. `InProgress`, `WaitingOnCustomer` and `Resolved` require an assignee.

  | From | Allowed targets |
  |---|---|
  | `Open` | `InProgress`, `Closed` |
  | `InProgress` | `WaitingOnCustomer`, `Resolved` |
  | `WaitingOnCustomer` | `InProgress`, `Resolved` |
  | `Resolved` | `InProgress`, `Closed` |
  | `Closed` | none |

- **Comments** can be public or internal. Only admins and agents can write or read internal comments; clients get public ones only. A closed ticket receives no comments.
- **Audit history** records the ticket's creation, assignment and status changes with the user and time of each change. Comments are not audited.
- **Concurrent changes** to the same ticket are detected with optimistic concurrency; the losing request answers `409`.

### Authentication

- **Passwords** are hashed with PBKDF2-HMAC-SHA512, 210,000 iterations and a random salt (ASP.NET Core Identity's `PasswordHasher`). Older hashes are upgraded on the next successful login.
- **Access tokens** are JWTs signed with HS256 that expire after 15 minutes. They carry `sub`, `email`, `name`, `role` and `jti`.
- **Refresh tokens** are 256-bit random values that expire after 7 days. Only their SHA-256 hash is stored. Each refresh rotates the token; presenting an already rotated token revokes every active session of that user.
- **Authorization** uses the policies `AdminOnly`, `StaffOnly` (Admin and Agent) and `ClientOnly`. Endpoints require an authenticated user unless they opt out explicitly.
- **Errors** use `application/problem+json`. Login answers the same `401` for an unknown email, a wrong password and an inactive account.

Known limitations:

- An access token that was already issued stays valid until it expires, for up to 15 minutes, even after the user is deactivated or their role changes. Access tokens are not checked against the database on each request; the short lifetime bounds this window, and refreshing is rejected immediately for inactive users.
- There is no rate limiting or lockout on the authentication endpoints yet ([#20](https://github.com/yorkael/Helpdesk/issues/20)).
- There is no endpoint to create agents or admins yet ([#19](https://github.com/yorkael/Helpdesk/issues/19)); see [Staff users](#staff-users).

### Operations

| Method | Route | Access | Result |
|---|---|---|---|
| `GET` | `/health` | anonymous | `200` with `Healthy` when PostgreSQL accepts a connection, `503` with `Unhealthy` otherwise, as plain text |

- The check only opens a connection to PostgreSQL, with a 3-second limit that covers the TCP connection, the startup handshake and authentication. It does not check the schema or pending migrations.
- The body is only the overall status. The reason for a failure goes to the API log, never to the caller.
- In the Docker stack, the `api` container uses it as its healthcheck: every 10 seconds, unhealthy after 3 failed probes, with a 30-second start period. The `web` service starts only once `api` is healthy.

```bash
curl -i http://127.0.0.1:8080/health   # full Docker stack
curl -i http://localhost:5038/health   # dotnet run
```

Known limitations, measured with Docker Desktop:

- While the database is down, every check logs an error with its stack trace: about 14 lines when the database does not answer and about 19 when its container is stopped. At one probe every 10 seconds that is roughly 121,000 to 166,000 lines a day. While the database is healthy, the checks log nothing.
- When the `db` container is stopped, its name no longer resolves, and the failed DNS lookup, which the 3-second limit does not cover, makes `/health` take about 8 seconds. The container probe gives up after 4 seconds and the API is marked unhealthy after about 33 seconds.
- `/health` is only served on the API port. On the frontend port (http://127.0.0.1:8081/health) nginx answers `200` with `index.html`, its single-page app fallback, because it proxies only `/api/`.
- If the API is unhealthy when compose has to start `web` (for example, because it cannot reach the database), `docker compose up` stops with `dependency failed to start: container ... is unhealthy` and `web` is created but not started, although nginx could serve the static files on its own.
- Docker only reports the API as unhealthy; it does not restart it (`restart: unless-stopped` acts only when the process exits). The API recovers on its own when the database returns.
- The endpoint is anonymous and has no rate limiting ([#20](https://github.com/yorkael/Helpdesk/issues/20)).

## Roadmap

- Specification, architecture and backlog: [docs/Helpdesk_Especificacion_y_Plan.pdf](docs/Helpdesk_Especificacion_y_Plan.pdf) (in Spanish)
- Progress by sprint: [GitHub Project](https://github.com/users/yorkael/projects/1)
- Sprint 3: containerized stack with Docker Compose (done, [#11](https://github.com/yorkael/Helpdesk/issues/11)) and its health check (done, [#34](https://github.com/yorkael/Helpdesk/issues/34)); CI with GitHub Actions ([#12](https://github.com/yorkael/Helpdesk/issues/12)), the React UI ([#13](https://github.com/yorkael/Helpdesk/issues/13)) and deployment ([#14](https://github.com/yorkael/Helpdesk/issues/14)) are pending.

## License

[MIT](LICENSE)
