# Helpdesk

A support ticket system built as a public portfolio project: a layered ASP.NET Core backend and a React frontend.

**Status:** Under construction (Sprint 1).

## Tech stack

| Area | Technologies |
|---|---|
| Backend | ASP.NET Core on .NET 10, C#, EF Core, FluentValidation |
| Authentication | JWT bearer access tokens, rotating refresh tokens, role-based policies |
| Database | PostgreSQL 18 |
| Tests | xUnit, Testcontainers, WebApplicationFactory |
| Frontend | React, TypeScript, Vite, Tailwind CSS |
| Tooling | pnpm, oxlint |

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
│   ├── src/
│   │   ├── Helpdesk.Api/
│   │   ├── Helpdesk.Application/
│   │   ├── Helpdesk.Domain/
│   │   └── Helpdesk.Infrastructure/
│   └── tests/
│       └── Helpdesk.Tests/
├── frontend/                   # Vite + React + TypeScript + Tailwind CSS
├── docs/                       # project specification and plan
├── .github/workflows/          # CI workflows (empty for now)
├── .env.example
├── docker-compose.yml          # local PostgreSQL
├── dotnet-tools.json           # local .NET tools (dotnet-ef)
└── global.json                 # pins the .NET SDK to 10.x
```

## Getting started

### Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) 22.12 or later
- [pnpm](https://pnpm.io/installation) (the version is pinned in `frontend/package.json`)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (local database and integration tests)

### Environment variables

Copy the example file and fill in your local values:

```bash
cp .env.example .env
```

`.env` is ignored by Git. `docker compose` reads the `POSTGRES_*` variables from it.

### Local database

From the repository root:

```bash
docker compose up -d                 # PostgreSQL 18 on 127.0.0.1:${POSTGRES_PORT}
dotnet tool restore                  # installs dotnet-ef from dotnet-tools.json
```

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

Migrations are applied explicitly; the API does not migrate the database on startup.

### Backend

From the repository root:

```bash
dotnet build backend/Helpdesk.sln
dotnet test backend/Helpdesk.sln
dotnet run --project backend/src/Helpdesk.Api
```

`dotnet test` requires Docker to be running: the persistence and API tests start a disposable PostgreSQL 18 container with Testcontainers. The API tests host the application in memory with their own settings and a random signing key, so they do not need user secrets.

The API listens on http://localhost:5038 and fails at startup if the connection string or the JWT settings are missing or invalid. In Development the OpenAPI document is served at http://localhost:5038/openapi/v1.json.

### Frontend

```bash
cd frontend
pnpm install
pnpm dev
```

The app runs on http://localhost:5173.

## Authentication

| Endpoint | Access | Result |
|---|---|---|
| `POST /api/auth/register` | anonymous | `201` with the new user; public sign-up always creates a `Client` |
| `POST /api/auth/login` | anonymous | `200` with an access token and a refresh token |
| `POST /api/auth/refresh` | anonymous | `200` with a new token pair; the refresh token used is revoked |
| `POST /api/auth/logout` | anonymous | `204`, also for unknown or already revoked tokens |
| `GET /api/auth/me` | authenticated | `200` with the caller as described by the access token |

- **Passwords** are hashed with PBKDF2-HMAC-SHA512, 210,000 iterations and a random salt (ASP.NET Core Identity's `PasswordHasher`). Older hashes are upgraded on the next successful login.
- **Access tokens** are JWTs signed with HS256 that expire after 15 minutes. They carry `sub`, `email`, `name`, `role` and `jti`.
- **Refresh tokens** are 256-bit random values that expire after 7 days. Only their SHA-256 hash is stored. Each refresh rotates the token; presenting an already rotated token revokes every active session of that user.
- **Authorization** uses the policies `AdminOnly`, `StaffOnly` (Admin and Agent) and `ClientOnly`. Endpoints require an authenticated user unless they opt out explicitly.
- **Errors** use `application/problem+json`. Login answers the same `401` for an unknown email, a wrong password and an inactive account.

Known limitations:

- An access token that was already issued stays valid until it expires, for up to 15 minutes, even after the user is deactivated or their role changes. Access tokens are not checked against the database on each request; the short lifetime bounds this window, and refreshing is rejected immediately for inactive users.
- There is no rate limiting or lockout on the authentication endpoints yet ([#20](https://github.com/yorkael/Helpdesk/issues/20)).

## Roadmap

- Specification, architecture and backlog: [docs/Helpdesk_Especificacion_y_Plan.pdf](docs/Helpdesk_Especificacion_y_Plan.pdf) (in Spanish)
- Progress by sprint: [GitHub Project](https://github.com/users/yorkael/projects/1)

## License

[MIT](LICENSE)
