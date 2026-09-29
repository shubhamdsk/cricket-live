# Cricket Live — Architecture

How the code is laid out **today**. The intended end state is in
[system-design.md](./system-design.md) and [`project-plan.md`](../project-plan.md); this document
describes what exists, and grows as each sprint lands.

Last updated: Sprint 3.

---

## System shape today

```text
React (Vite dev server, :5173)
        │  HTTP, CORS allow-list
        ▼
ASP.NET Core API (:5140)
        │   ├── GET /api/health
        │   ├── GET /api/matches/{live,upcoming,recent,series,{matchId}}
        │   └── GET /api/matches/{matchId}/stream   ← SSE
        │
        │   LiveMatchPoller ──► IMatchBroadcaster ──► connected clients
        │   (only while someone is subscribed)
        │
        │  one call per refresh, in-memory cache, daily budget guard
        ▼
api.cricapi.com (CricketData)
        │
        └─► every finished match that passes ──► SQLite (cricket-live.db)
```

No Redis. SQLite holds the match archive, which is a file rather than a service
([D-017](./decisions.md)); PostgreSQL is still the deployment target. Redis waits for a second API
instance ([D-014](./decisions.md)). Every screen renders provider data end to end and no mock data
remains.

**Results outlive the provider's window.** The window is a few days wide, so without keeping
anything, "recent results" would mean "since the day before yesterday" and a finished match page
would stop resolving. An `ICricketDataProvider` decorator writes every completed match it sees to
the archive on the way past, which is why `recent` is the one paged endpoint and why the archive
fills whether or not anyone is watching live cricket.

**One filter reaches both stores.** `MatchFilter` describes status, a UTC range and a series once;
the window is narrowed in memory and the archive in SQL. Two implementations are unavoidable —
filtering a page after reading it leaves holes in it, and the count stops agreeing with the page —
but one description keeps them from drifting apart ([D-018](./decisions.md)).

**Two loops, and only one of them costs anything.** Browsers attach over SSE, which never touches
the provider, so a thousand connected clients cost exactly what one does. The provider is reached
only by the poller, and only while at least one client is subscribed — and even then each tick
passes through the same five-minute cache the HTTP endpoints use, capping spend at twelve calls an
hour regardless of tick rate. Idle days cost nothing.

A second, optional source sits beside the provider and supplies one field:

```text
        │   LiveMatchPoller ──► IMatchBroadcaster ──► connected clients
        │        │
        │        └─► IMatchEnrichmentProvider ──► cricbuzz.com   (off by default,
        │            batters at the crease only                   hand-mapped matches only)
```

It is not a second provider in any meaningful sense: it cannot list matches, so it can never
replace the first. It contributes `currentBatters` and nothing else, because that was the only
field it produced reliably when tested against real pages. The reasoning, the evidence and the
terms question are in [D-015](./decisions.md).

The match list pages still poll over plain HTTP once a minute; only the match detail page streams.
That is deliberate rather than unfinished: those requests hit our own cache, and a stream per list
page would keep the poller awake for people browsing fixtures rather than watching cricket.

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
│   ├── CricketLive.Api              controllers, middleware, DI wiring, ApiRoutes
│   ├── CricketLive.Application      DTOs, abstractions, the response envelope, paging
│   ├── CricketLive.Domain           entities — empty until there is something to own
│   └── CricketLive.Infrastructure   CricketData/, Cricbuzz/, Live/, Persistence/
└── tests/
    ├── CricketLive.Api.Tests             xUnit
    ├── CricketLive.Application.Tests     xUnit — service and paging behaviour
    └── CricketLive.Infrastructure.Tests  xUnit — mapper tests over captured fixtures
```

`ApiRoutes` holds every route prefix, because matches are served by two controllers — one
returning the envelope, one streaming frames — and that is exactly where the same string gets
typed twice and only one copy gets updated. `PageRequest` holds the page bounds for the same
reason: a controller default and a service default that disagree are a bug nobody notices.

Dependencies point inward: `Api` → `Application` and `Infrastructure`; `Infrastructure` →
`Application`; `Application` → `Domain`.

`Domain` is still deliberately empty. It exists because the shape is known and adding a project
later means touching every reference; it stays empty rather than being filled with speculative types.

### Provider isolation

```text
backend/src/CricketLive.Infrastructure/CricketData/
├── Models/CricketDataModels.cs   internal — the provider's vocabulary, never ours
├── CricketDataClient.cs          the one place an HTTP request leaves for the provider
├── CricketDataMatchMapper.cs     absorbs everything the provider gets wrong or omits
├── InningsLabel.cs               reads the batting side out of a free-text innings label
├── CricketDataHitBudget.cs       keeps us inside 100 calls a day
├── CricketDataProvider.cs        ICricketDataProvider, caching, single-flight
└── CricketDataOptions.cs         bound and validated at startup
```

Every provider model is `internal`, so `Api` cannot name one even by accident.
`InternalsVisibleTo` opens them to `CricketLive.Infrastructure.Tests` and nothing else.

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
├── features/       matches/ (api, components, hooks, types, utils)
├── pages/          Home, Live, Matches, MatchDetails, NotFound
├── services/       apiClient, endpoints
├── store/          uiStore
├── types/          api envelope
├── utils/          cn
└── index.css       Tailwind theme tokens
```

React 19, Vite, Tailwind CSS v4, React Router, TanStack Query, Zustand, Lucide. The `@/` alias
points at `src/`. Details in [frontend.md](./frontend.md).

**Three layers, and each knows one thing.** `services/endpoints.ts` is the only file containing a
path; `services/apiClient.ts` is the only file that knows the base URL, unwraps the envelope and
turns a failure into an `ApiError`; `features/matches/api/matchesApi.ts` is the only file that
calls the matches API, and every hook and component goes through it. The live stream is the case
that proves the split is worth having: `EventSource` cannot use `fetch`, so it needs the URL
without the request — it takes the same path from `endpoints` and the same base URL from
`apiClient` rather than assembling a second one by hand.

---

## What arrives when

| Sprint | Added |
| --- | --- |
| 4 | Frontend calls the real endpoints; UI sections the provider cannot fill are removed |
| 5 | Redis, `LiveMatchBackgroundService`, SSE endpoint, `useMatchLiveStream` |
| 6 | Scorecard, commentary, and stats endpoints |
| 7 | PostgreSQL, EF Core, migrations, series/teams/players/search |
| 8 | Rate limiting, health checks, security headers, code splitting, deployment |

---

## Local development

The API needs a CricketData key before it will start; see the README. A missing key fails options
validation at startup rather than surfacing as a 500 later.

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
