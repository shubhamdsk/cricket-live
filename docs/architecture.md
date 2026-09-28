# Cricket Live — Architecture

How the code is laid out **today**. The intended end state is in
[system-design.md](./system-design.md) and [`project-plan.md`](../project-plan.md); this document
describes what exists, and grows as each sprint lands.

Last updated: Sprint 2.

---

## System shape today

```text
React (Vite dev server, :5173)
        │  HTTP, CORS allow-list
        ▼
ASP.NET Core API (:5140)
        │
        └── GET /api/health
```

No cricket provider, no Redis, no PostgreSQL, no background service, no SSE. Those arrive in
Sprints 3, 5, and 7. The frontend currently renders mock cricket data served through the function
signatures the API layer will keep (see [D-009](./decisions.md)).

### Ports

| Process | URL |
| --- | --- |
| Frontend dev server | `http://localhost:5173` |
| API | `http://localhost:5140` |
| Swagger UI | `http://localhost:5140/swagger` |
| OpenAPI document | `http://localhost:5140/openapi/v1.json` |

The frontend reads its base URL from `VITE_API_BASE_URL`. The API reads its CORS allow-list from
`Cors:AllowedOrigins`, which is `http://localhost:5173` in Development and empty by default
elsewhere.

---

## Backend

Solution `backend/CricketLive.slnx`, the XML solution format the .NET 10 SDK emits.

```text
backend/
├── src/
│   ├── CricketLive.Api              controllers, middleware, DI wiring
│   ├── CricketLive.Application      DTOs, abstractions, the response envelope
│   ├── CricketLive.Domain           entities — empty until there is something to own
│   └── CricketLive.Infrastructure   provider, cache, persistence — empty until Sprint 3
└── tests/
    └── CricketLive.Api.Tests        xUnit
```

Dependencies point inward: `Api` → `Application` and `Infrastructure`; `Infrastructure` →
`Application`; `Application` → `Domain`.

`Domain` and `Infrastructure` are deliberately empty. They exist because the shape is known and
adding a project later means touching every reference; they stay empty rather than being filled with
speculative types.

### Pipeline

```text
ExceptionHandlingMiddleware  →  OpenAPI + Swagger (Development)  →  CORS  →  Controllers
```

The exception middleware is outermost so it catches everything after it, and it rethrows rather than
writing an envelope once a response has started — which is what will keep it safe around SSE
([D-005](./decisions.md)).

HTTPS redirection runs outside Development only ([D-008](./decisions.md)).

---

## Frontend

```text
frontend/src/
├── app/            App, providers, router
├── components/     common/, layout/, match/
├── features/       matches/ (api, components, hooks, mocks, types, utils), series/
├── pages/          Home, Live, Matches, MatchDetails, NotFound
├── services/       apiClient
├── store/          uiStore
├── types/          api envelope
├── utils/          cn
└── index.css       Tailwind theme tokens
```

React 19, Vite, Tailwind CSS v4, React Router, TanStack Query, Zustand, Lucide. The `@/` alias
points at `src/`. Details in [frontend.md](./frontend.md).

---

## What arrives when

| Sprint | Added |
| --- | --- |
| 3 | `Infrastructure/SportScore`, `ICricketDataProvider`, DTOs, mappers, match endpoints |
| 5 | Redis, `LiveMatchBackgroundService`, SSE endpoint, `useMatchLiveStream` |
| 6 | Scorecard, commentary, and stats endpoints |
| 7 | PostgreSQL, EF Core, migrations, series/teams/players/search |
| 8 | Rate limiting, health checks, security headers, code splitting, deployment |

---

## Local development

```powershell
# Backend
cd backend
dotnet run --project src/CricketLive.Api

# Frontend
cd frontend
npm install
npm run dev
```

Checks before calling work done:

```powershell
cd backend;  dotnet build; dotnet test
cd frontend; npm run lint; npm run format:check; npm run build
```
