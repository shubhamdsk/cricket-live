# 🏏 Cricket Live

**Live site: [cricket-live-shubhamdsk1.vercel.app](https://cricket-live-shubhamdsk1.vercel.app/)**

A responsive cricket score platform built with React, TypeScript, Tailwind CSS and ASP.NET Core.
Live scores update without a refresh, and every match has its own page with a full scorecard.

## Features

* **Live scores** that update in place over Server-Sent Events
* **Home, Live, Matches, Series and Teams** sections, always one tap away on mobile
* **Match pages** with batting and bowling cards, fall of wickets and partnerships
* **Results archive** that keeps finished matches past the provider's short window
* **Search** across matches, teams and series
* **Outage-tolerant:** when the score provider is down, the site serves the last data it stored
  and labels how old it is
* **Installable** as an app, with the last scores you loaded still readable offline
* **Match link previews** on WhatsApp, X and Slack show the teams, scores and result
* Light and dark themes

Coverage is deliberately narrow: matches between ICC Full Member nations, plus India's own
competitions (IPL, WPL and domestic). See [D-041](docs/decisions.md).

## Data sources

| Source | Used for | Free limit |
| --- | --- | --- |
| [CricketData](https://cricketdata.org) | Live scores, fixtures, results, series, teams | 100 calls / day |
| [Cricbuzz Cricket on RapidAPI](https://rapidapi.com/cricketapilive/api/cricbuzz-cricket) | Full scorecards, only when a reader presses the button | 200 calls / month |

Nothing polls the scorecard source; a finished match's card is fetched once and cached. Ball-by-ball
commentary is not built, because following one match would use the whole monthly allowance in
minutes ([D-027](docs/decisions.md)). The scorecard source is not a licensed Cricbuzz product; read
[D-031](docs/decisions.md) and [D-043](docs/decisions.md) before enabling it anywhere public.

## Tech stack

```text
frontend/   React 19, TypeScript, Vite, Tailwind CSS, TanStack Query, Zustand
backend/    ASP.NET Core (.NET 10): Api, Application, Domain, Infrastructure
            EF Core with PostgreSQL (Neon) in production, SQLite locally
docs/       Architecture, decisions, deployment and sprint documentation
```

Hosting: frontend on **Vercel**, API on **Render**, archive database on **Neon**. All on free plans.

## Getting started

### Prerequisites

```text
Node.js 22+
.NET SDK 10
```

### 1. API keys

Keys go in .NET user secrets, never in `appsettings.json`, which is tracked by git.

**CricketData (required).** Sign up at [cricketdata.org](https://cricketdata.org/member.aspx); the
key is the GUID shown under your plan.

```bash
cd backend
dotnet user-secrets set "CricketData:ApiKey" "your-guid" --project src/CricketLive.Api
```

**Scorecards (optional).** Subscribe to the free plan of the
[Cricbuzz Cricket listing](https://rapidapi.com/cricketapilive/api/cricbuzz-cricket), then:

```bash
dotnet user-secrets set "CricbuzzApi:ApiKey" "your-rapidapi-key" --project src/CricketLive.Api
dotnet user-secrets set "CricbuzzApi:Enabled" "true" --project src/CricketLive.Api
dotnet user-secrets set "Cricbuzz:AutoResolve" "true" --project src/CricketLive.Api
```

`Cricbuzz:AutoResolve` pairs our match ids with Cricbuzz's by reading cricbuzz.com's listing pages.
Without it, no match can be paired and no scorecard is ever fetched. To pair by hand instead, set
`Cricbuzz:MatchIds:<our-id>` to the Cricbuzz match id.

In deployed environments use environment variables, with a double underscore for each level:
`CricketData__ApiKey`, `CricbuzzApi__ApiKey`, and so on.

### 2. Run

Backend and frontend in two terminals:

```bash
cd backend
dotnet run --project src/CricketLive.Api
```

```bash
cd frontend
npm install
npm run dev
```

```text
App       http://localhost:5173
API       http://localhost:5140
Swagger   http://localhost:5140/swagger
```

The frontend reads the API address from `VITE_API_BASE_URL`; `frontend/.env.development` already
points at the local API.

## Checks

These are what CI runs on every pull request.

```bash
# frontend
npm run lint
npm run typecheck
npm run format:check
npm test        # unit and component tests (Vitest)
npm run build
npm run e2e     # phone-size browser tests (Playwright; run `npx playwright install chromium` once)

# backend
dotnet build
dotnet test
```

## Deployment

Work goes into `develop` through feature-branch pull requests and is released by merging `develop`
into `master`. Pushing to `master` deploys both the frontend (Vercel) and the API (Render).
Both branches require the `backend`, `frontend` and `scan` checks to pass.

**Keeping the API awake.** Render's free plan stops the API after about 15 minutes without
traffic, and the next visitor waits 30–60 seconds while it starts. To prevent that, a free
[cron-job.org](https://cron-job.org) job named **Cricket Live keep-awake** calls
`https://cricket-live-api-qwo6.onrender.com/api/health/live` every 5 minutes.

* That endpoint runs no checks and calls no provider, so the ping costs no CricketData or RapidAPI
  calls. Do not point it at `/api/health/ready`, which queries the database and would keep Neon
  awake too.
* The job's history on cron-job.org should show `200 OK` every 5 minutes; it emails on failure.
* The GitHub `keep-warm.yml` workflow pings the same endpoint as a backup, but GitHub runs free
  schedules only every few hours, so it cannot keep the API awake on its own.

Full details in [docs/deployment.md](docs/deployment.md).

## Documentation

* [Project plan](project-plan.md): architecture and requirements
* [Sprint plan](docs/sprint-plan.md): what was built, in what order
* [Architecture](docs/architecture.md) and [system design](docs/system-design.md)
* [API reference](docs/api.md)
* [Deployment](docs/deployment.md)
* [Security](docs/security.md)
* [Decisions](docs/decisions.md): what was chosen, why, and what it costs
