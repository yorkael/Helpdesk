# Helpdesk

Sistema de tickets de soporte. Proyecto público de portafolio de Yorkael, construido por etapas con Claude Code. La especificación completa (requisitos, arquitectura, backlog por sprints) está en `docs/Helpdesk_Especificacion_y_Plan.pdf`. Léela antes de proponer cualquier cosa.

## Stack
- Backend: ASP.NET Core .NET 10 (LTS), C#, EF Core, PostgreSQL, FluentValidation, xUnit, Testcontainers
  (El PDF de especificación dice .NET 8; esa parte quedó desactualizada. Usar .NET 10: .NET 8 y 9 terminan soporte el 10 de noviembre de 2026.)
  Planned: Serilog (requisito no funcional del PDF, sin HU asignada).
- Frontend: React 19, TypeScript estricto, Vite, Tailwind CSS 4 (tokens en `frontend/src/styles/tokens.css`, sin la paleta por defecto), React Router (`RouterProvider` se importa de `react-router/dom`), TanStack Query para el estado de servidor, oxlint; pruebas con Vitest, Testing Library, user-event y jsdom (fetch mockeado con `vi.fn`, sin MSW)
  Zustand solo para la sesión (`frontend/src/lib/auth/sessionStore.ts`).
- DevOps: Dockerfiles multi-stage (`backend/Dockerfile` con targets `runtime` y `migrator`, `frontend/Dockerfile` con nginx sin root) y docker-compose con PostgreSQL, migrate (EF migrations bundle), API y frontend (HU-11). Healthcheck de la api sobre `GET /health` (#34): bash con `/dev/tcp` porque la imagen no trae curl ni wget, envuelto en `timeout 4` porque el timeout de Docker deja procesos `head` colgados si la API no responde; `web` espera a que la api esté healthy
  CI con GitHub Actions (HU-12): `.github/workflows/ci.yml`, en PR y push a `main`, jobs `backend` (restore, build Release, test con Testcontainers) y `frontend` (install, lint, test con Vitest, build) en `ubuntu-24.04`. `backend` y `frontend` son checks obligatorios del ruleset "Protect main": no renombrar los jobs sin cambiar el ruleset a la vez (si no, todos los PR quedan esperando un check que nunca llega). Sin filtros de paths (un check obligatorio que no corre bloquea el merge), `permissions: contents: read`, sin secretos, nunca `pull_request_target`. Acciones fijadas por SHA de commit (no el del tag anotado) con `# vX.Y.Z`, versiones con al menos dos semanas. Versiones leídas del repo: SDK de `global.json`, Node de `frontend/.nvmrc` (duplicada en `frontend/Dockerfile`: actualizar juntas), pnpm del `packageManager`. Caché solo de pnpm (la de NuGet exige `packages.lock.json`, que no hay)
- Despliegue (planned, HU-14): backend en Railway, frontend en Vercel

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
- Dependencias nuevas: versión exacta en `package.json` (sin `^` ni `~`), publicada hace al menos dos semanas.

## Reglas del frontend
- `className` en los primitivos de `src/ui` solo para disposición (ancho, márgenes, posición), nunca colores: no hay twMerge, así que una clase de color del consumidor competiría con la del primitivo y ganaría la que Tailwind emita después, no la última escrita.
- El token `subtle` sobre `line` no cumple contraste (4.14:1): no se combinan.
- TypeScript no comprueba los atributos JSX con guion (`aria-*`, `data-*`), así que `Omit` no los protege. La garantía es el orden de props en ejecución (los del primitivo después del spread del consumidor) más sus pruebas.
- Formularios (desde la etapa 2 de HU-13): protección contra doble envío en el propio manejador, no solo con el botón en loading, y foco al primer campo inválido tras un envío fallido.
- La galería `/dev/gallery` solo existe en desarrollo: la condición `import.meta.env.DEV` va en línea a nivel de módulo en `src/app/router.ts` para que el build de producción elimine el `import()`. No moverla a una función con parámetro.
- Sesión (HU-13 etapa 2; detalle y limitaciones en la sección "Session handling" del README):
  - El access token vive solo en el closure de `src/lib/auth/sessionManager.ts`, nunca en Zustand, en estado de React ni en storage. El refresh token va en `localStorage` (`helpdesk.refreshToken`).
  - El refresh token se lee y se reemplaza solo dentro del Web Lock `helpdesk-refresh`, y el nuevo se guarda antes de soltarlo. Single-flight dentro de la pestaña.
  - Nunca reintentar un refresh por cuenta propia tras un fallo que no sea 401 (red, timeout, 5xx, 429, respuesta sin tokens): si el servidor ya rotó el token, presentarlo otra vez revoca todas las sesiones del usuario. Dos refresh con el mismo token también revocan todo salvo que lleguen casi a la vez ([#43](https://github.com/yorkael/Helpdesk/issues/43)).
  - Solo un 401 del refresh cierra la sesión; un 403 nunca. Motivos: `expired` (conserva `from`, con aviso), `signedOut` (sin `from`, sin aviso) y `endedElsewhere` (evento `storage` al borrarse el token en otra pestaña, por Sign out, por expiración o por un storage vaciado; conserva `from`, con aviso).
  - Los guards son solo UX: la API autoriza cada petición.
  - Limitaciones aceptadas: la reutilización de un refresh revoca todo (#43); el Retry tras una respuesta perdida o un timeout tardío puede revocar todo; sin Web Locks el respaldo solo serializa dentro de la pestaña; una sola sesión por navegador y un login en otra pestaña no se propaga hasta el siguiente refresh; un login sobre una sesión existente deja huérfana la anterior hasta que caduca (7 días); `/me` sale de los claims (hasta 15 min de retraso en cambios de rol); el access token sigue valiendo tras el logout hasta que caduca (15 min); `/me` no se valida; riesgo de XSS aceptado en v1 (cookie httpOnly en [#44](https://github.com/yorkael/Helpdesk/issues/44), CSP con HU-14); el 403 de un Client en una ruta de staff queda sin verificar a mano hasta que exista esa ruta.

## Fuera de alcance
- Este proyecto es independiente de AgroSystem (SaaS privado y comercial). No reutilizar su código, nombres, dominio ni datos.
- Licencia MIT.

## Idioma
- Conversación conmigo en español.
- README, mensajes de commit, nombres de código y documentación del repo en inglés.

## Comandos (completar al crearlos)
- Fijar el SDK con `global.json` (SDK 10.x).
- Compilar: `dotnet build backend/Helpdesk.sln`
- Pruebas: `dotnet test backend/Helpdesk.sln` (requiere Docker en marcha: las pruebas de persistencia y de API usan Testcontainers con `postgres:18`; la fábrica de pruebas de API inyecta su propia configuración y clave aleatoria, no usa user-secrets)
- Pruebas unitarias (Application y Domain, sin Docker): `dotnet test backend/Helpdesk.sln --filter "FullyQualifiedName~Helpdesk.Tests.Application|FullyQualifiedName~Helpdesk.Tests.Domain"` (`ApplicationTestIsolationTests` falla si una prueba de esos namespaces usa un fixture de base de datos)
- Ejecutar la API: `dotnet run --project backend/src/Helpdesk.Api` (http://localhost:5038; en Development, OpenAPI en http://localhost:5038/openapi/v1.json). Salud: `GET /health`, anónimo, `200 Healthy` o `503 Unhealthy` según PostgreSQL acepte una conexión (límite de 3 s en el `Timeout` de Npgsql dentro de `DatabaseHealthCheck`; no comprueba esquema ni migraciones)
- Base de datos local (solo la base, sin clave JWT): `docker compose up -d db` (PostgreSQL 18 en `127.0.0.1:${POSTGRES_PORT}`, variables en `.env`). PostgreSQL debe tener ICU: el listado de categorías y el de agentes ordenan con la collation `und-x-icu` (la imagen oficial de PostgreSQL 18 la trae)
- Stack completo: `docker compose up --build` (requiere `Jwt__SigningKey` en `.env`; web en http://127.0.0.1:8081 con proxy de `/api/`, API en http://127.0.0.1:8080 en Production, sin OpenAPI; web arranca cuando la api está healthy; si la api está unhealthy, p. ej. porque no alcanza la base, `up` falla con `dependency failed to start` y web no arranca). `/health` solo en el puerto de la API: por el del web, nginx devuelve el `index.html`. Detener con `docker compose down`; nunca `down -v` en el proyecto por defecto: borra `pgdata`, la base local
- Pruebas del compose: siempre con un proyecto y un env-file aparte para no tocar la base local, p. ej. `docker compose -p helpdesk-verify --env-file <archivo fuera del repo> up --build -d` con `POSTGRES_PORT`, `API_PORT` y `WEB_PORT` propios; limpiar con `docker compose -p helpdesk-verify down -v`
- Herramientas locales: `dotnet tool restore` (instala `dotnet-ef` desde `dotnet-tools.json`)
- Cadena de conexión local: user-secrets de Api, clave `ConnectionStrings:DefaultConnection`; fuera de local, variable `ConnectionStrings__DefaultConnection`
- Clave de firma JWT local (mínimo 32 bytes aleatorios en base64; sin ella la API y `dotnet ef` no arrancan): user-secrets de Api, clave `Jwt:SigningKey`; fuera de local, variable `Jwt__SigningKey`. Generarla en PowerShell 5.1 o 7: `$bytes = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes); dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String($bytes)) --project backend/src/Helpdesk.Api`; alternativa: `openssl rand -base64 48`
- Aplicar migraciones: `dotnet ef database update --project backend/src/Helpdesk.Infrastructure --startup-project backend/src/Helpdesk.Api`
- Nueva migración: `dotnet ef migrations add <Name> --project backend/src/Helpdesk.Infrastructure --startup-project backend/src/Helpdesk.Api --output-dir Persistence/Migrations`
- Frontend (gestor de paquetes: pnpm, no mezclar con npm): `cd frontend && pnpm install && pnpm dev`; lint con `pnpm lint` (oxlint con `--deny-warnings`); pruebas con `pnpm test` (Vitest); galería de primitivos en http://localhost:5173/dev/gallery, solo con `pnpm dev`. Node `^22.22.2 || ^24.15.0 || >=26.0.0` (el rango que exige jsdom 30; excluye Node 23 y 25; CI y Docker usan `frontend/.nvmrc`)
- Validar el workflow antes de subirlo: `docker run --rm -v "$(pwd):/repo" -w /repo rhysd/actionlint:1.7.12` desde la raíz (en Git Bash de Windows: `MSYS_NO_PATHCONV=1` y `$(pwd -W)`)
- Reproducir CI en local: backend `dotnet restore backend/Helpdesk.sln`, `dotnet build backend/Helpdesk.sln --no-restore --configuration Release`, `dotnet test backend/Helpdesk.sln --no-build --configuration Release`; frontend `pnpm install --frozen-lockfile`, `pnpm lint`, `pnpm test`, `pnpm build`
