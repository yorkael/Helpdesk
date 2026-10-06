# Helpdesk

Sistema de tickets de soporte. Proyecto público de portafolio de Yorkael, construido por etapas con Claude Code. La especificación completa (requisitos, arquitectura, backlog por sprints) está en `docs/Helpdesk_Especificacion_y_Plan.pdf`. Léela antes de proponer cualquier cosa.

## Stack
- Backend: ASP.NET Core .NET 10 (LTS), C#, EF Core, PostgreSQL, FluentValidation, Serilog, xUnit
  (El PDF de especificación dice .NET 8; esa parte quedó desactualizada. Usar .NET 10: .NET 8 y 9 terminan soporte el 10 de noviembre de 2026.)
- Frontend: React, TypeScript, Vite, Tailwind CSS, Zustand
- DevOps: Docker, docker-compose, GitHub Actions
- Despliegue: backend en Railway, frontend en Vercel

## Arquitectura (backend)
Capas: `Helpdesk.Api` -> `Helpdesk.Application` -> `Helpdesk.Domain`; `Helpdesk.Infrastructure` implementa las interfaces de Application. Domain no depende de ningún otro proyecto ni de paquetes externos. Pruebas en `Helpdesk.Tests`.

## Cómo trabajamos
- Una historia de usuario (HU) a la vez, en el orden del backlog. No avances a la siguiente sin que yo lo pida.
- Antes de escribir código de cada HU: explica el plan y espera mi confirmación.
- Explica el porqué de cada decisión técnica, no solo el qué. Debo poder defenderla en una entrevista con mis palabras.
- Al cerrar cada HU, hazme 2 o 3 preguntas de entrevista sobre lo que construimos.
- Sigue el flujo de Git descrito abajo, sin excepciones (nunca commits directos a `main` una vez activada la protección).
- Nunca hagas commit ni push sin mi aprobación explícita. Antes de cada commit muéstrame `git status`, el diff y la salida de las verificaciones, y espera mi confirmación.
- Trabaja por etapas: al terminar cada una, detente y espera mi confirmación para seguir.

## Flujo de Git (GitHub Flow)
- `main` siempre estable. Una rama por HU: `feature/hu-N-short-description` (también `fix/`, `chore/`, `docs/`).
- Dentro de la rama: commits pequeños y frecuentes, en inglés, con Conventional Commits y alcance opcional:
  `feat(domain): add Ticket entity`, `test(application): cover ticket creation rules`.
- Al terminar la HU: pull request hacia `main`. Título en formato Conventional Commits y en inglés (será el mensaje final del commit). Descripción con: Summary, Changes, How to test, y `Closes #N` para cerrar el issue.
- Se fusiona con Squash and merge: queda un solo commit por HU en `main`, bien estructurado.
- Al cerrar un sprint: milestone completado y tag semántico (`v0.1.0` tras el Sprint 1, `v0.2.0` tras el Sprint 2, etc.).
- Mensajes de commit y PR siempre en inglés, en modo imperativo ("add", no "added").

## Reglas del código
- Nombres descriptivos, sin código muerto ni comentarios que repitan lo que ya dice el código.
- Sin secretos en el repositorio: variables de entorno y `.env.example`. Nunca subir `.env` ni `appsettings.Development.json` con credenciales.
- Todo endpoint de escritura valida con FluentValidation.
- Errores con respuestas consistentes (problem+json).
- Cada funcionalidad nueva lleva sus pruebas.

## Fuera de alcance
- Este proyecto es independiente de AgroSystem (SaaS privado y comercial). No reutilizar su código, nombres, dominio ni datos.
- Licencia MIT.

## Idioma
- Conversación conmigo en español.
- README, mensajes de commit, nombres de código y documentación del repo en inglés.

## Comandos (completar al crearlos)
- Fijar el SDK con `global.json` (SDK 10.x).
- Compilar: `dotnet build backend/Helpdesk.sln`
- Pruebas: `dotnet test backend/Helpdesk.sln` (requiere Docker en marcha: las pruebas de persistencia usan Testcontainers con `postgres:18`)
- Base de datos local: `docker compose up -d` (PostgreSQL 18 en `127.0.0.1:${POSTGRES_PORT}`, variables en `.env`)
- Herramientas locales: `dotnet tool restore` (instala `dotnet-ef` desde `dotnet-tools.json`)
- Cadena de conexión local: user-secrets de Api, clave `ConnectionStrings:DefaultConnection`; fuera de local, variable `ConnectionStrings__DefaultConnection`
- Aplicar migraciones: `dotnet ef database update --project backend/src/Helpdesk.Infrastructure --startup-project backend/src/Helpdesk.Api`
- Nueva migración: `dotnet ef migrations add <Name> --project backend/src/Helpdesk.Infrastructure --startup-project backend/src/Helpdesk.Api --output-dir Persistence/Migrations`
- Frontend (gestor de paquetes: pnpm, no mezclar con npm): `cd frontend && pnpm install && pnpm dev`
