# Helpdesk

A support ticket system built as a public portfolio project: a layered ASP.NET Core backend and a React frontend.

**Status:** Under construction (Sprint 1).

## Tech stack

| Area | Technologies |
|---|---|
| Backend | ASP.NET Core on .NET 10, C# |
| Tests | xUnit |
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
└── global.json                 # pins the .NET SDK to 10.x
```

## Getting started

### Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) 22.12 or later
- [pnpm](https://pnpm.io/installation) (the version is pinned in `frontend/package.json`)

### Environment variables

Copy the example file and fill in your local values:

```bash
cp .env.example .env
```

`.env` is ignored by Git. The variables are placeholders for upcoming stories; the current code does not read them yet.

### Backend

From the repository root:

```bash
dotnet build backend/Helpdesk.sln
dotnet test backend/Helpdesk.sln
dotnet run --project backend/src/Helpdesk.Api
```

The API listens on http://localhost:5038. There are no endpoints yet; in Development the OpenAPI document is served at http://localhost:5038/openapi/v1.json.

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
