# Helpdesk

A support ticket system built as a public portfolio project: a layered ASP.NET Core backend and a React frontend.

**Status:** Under construction (Sprint 1).

## Tech stack

| Area | Technologies |
|---|---|
| Backend | ASP.NET Core on .NET 10, C#, EF Core |
| Database | PostgreSQL 18 |
| Tests | xUnit, Testcontainers |
| Frontend | React, TypeScript, Vite, Tailwind CSS |
| Tooling | pnpm, oxlint |

## Architecture

The backend follows a layered architecture. Dependencies point inward, so business rules never depend on the web framework or the database.

| Project | Responsibility | References |
|---|---|---|
| `Helpdesk.Domain` | Entities, enums and business rules | none |
| `Helpdesk.Application` | Use cases, DTOs, validators and repository interfaces | Domain |
| `Helpdesk.Infrastructure` | Implementations of the Application interfaces (persistence) | Application, Domain |
| `Helpdesk.Api` | HTTP endpoints and composition root | Application, Infrastructure |
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

`dotnet test` requires Docker to be running: the persistence tests start a disposable PostgreSQL 18 container with Testcontainers.

The API listens on http://localhost:5038 and fails at startup if the connection string is missing. There are no endpoints yet; in Development the OpenAPI document is served at http://localhost:5038/openapi/v1.json.

### Frontend

```bash
cd frontend
pnpm install
pnpm dev
```

The app runs on http://localhost:5173.

## Roadmap

- Specification, architecture and backlog: [docs/Helpdesk_Especificacion_y_Plan.pdf](docs/Helpdesk_Especificacion_y_Plan.pdf) (in Spanish)
- Progress by sprint: [GitHub Project](https://github.com/users/yorkael/projects/1)

## License

[MIT](LICENSE)
