# 🏏 Cricket Live

A responsive real-time cricket score platform built with React, TypeScript, Tailwind CSS and ASP.NET Core: live scores, match details, full scorecards and SSE-powered updates.

Commentary is not built. The data exists and the reason is cost rather than availability, which is written up in [D-027](docs/decisions.md) rather than left for someone to rediscover.

## Documentation

* [Project plan](project-plan.md) — architecture and requirements
* [Sprint plan](docs/sprint-plan.md) — what gets built, in what order
* [Deployment](docs/deployment.md) — and the four things most likely to go wrong
* [Decisions](docs/decisions.md) — what was chosen, why, and what it costs

## Repository layout

```text
frontend/   React + TypeScript + Vite + Tailwind CSS
backend/    ASP.NET Core API (Api, Application, Domain, Infrastructure)
docs/       Project documentation
```

## Prerequisites

```text
Node.js 22+
.NET SDK 10
```

## Cricket data provider

Cricket data comes from [CricketData](https://cricketdata.org) (formerly CricAPI). The free plan is permanent, needs no card, and allows 100 requests per day.

1. Sign up at [cricketdata.org/member.aspx](https://cricketdata.org/member.aspx).
2. Confirm your email, then open the member area. The API key is a GUID shown under your plan details.
3. Store it in .NET user secrets — **never** in `appsettings.json`, which is tracked by git:

```bash
cd backend
dotnet user-secrets set "CricketData:ApiKey" "your-guid-here" --project src/CricketLive.Api
```

To confirm it was stored, and to remove it later:

```bash
dotnet user-secrets list --project src/CricketLive.Api
dotnet user-secrets remove "CricketData:ApiKey" --project src/CricketLive.Api
```

User secrets live outside the repository, at `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` on Windows. `Host.CreateApplicationBuilder` loads them automatically in Development, so no code change is needed.

In deployed environments set the environment variable `CricketData__ApiKey` instead — the double underscore is how .NET maps an environment variable onto a nested configuration key.

### Optional: scorecards

Scorecards come from a second source and the app runs fine without them. If you skip this, the scorecard section on a match page says there is none.

The source is the Cricbuzz listing on [RapidAPI](https://rapidapi.com/). It is a reseller of a scrape rather than a Cricbuzz product, and its free plan allows **200 requests per month** — not per day. That number is why the feature ships disabled and why the app never polls it.

**Read [D-031](docs/decisions.md) before turning it on, not just [D-027](docs/decisions.md).** The terms of all three parties have now been read, and neither route to Cricbuzz's data is licensed: Cricbuzz grants its site for "private viewing only", and RapidAPI's terms put the licence between you and the listing's publisher, who is not Cricbuzz. Running this locally against a handful of matches is one thing; serving it on a public site is the thing the grant excludes.

Pairing needs a second switch. `Cricbuzz:AutoResolve` reads Cricbuzz's own listing pages to match a fixture to its Cricbuzz id, and it is **off by default** — so with only `CricbuzzApi:Enabled` set, the scorecard pairs nothing unless you write ids into `Cricbuzz:MatchIds` by hand.

```bash
cd backend
dotnet user-secrets set "CricbuzzApi:ApiKey" "your-rapidapi-key" --project src/CricketLive.Api
```

Then enable it, either in `appsettings.Development.json` or with `CricbuzzApi__Enabled=true`:

```json
{ "CricbuzzApi": { "Enabled": true } }
```

For automatic pairing you must opt in separately, because it reads Cricbuzz's own listing pages rather than the gateway — a different party's permission, so a different switch:

```json
{ "CricbuzzApi": { "Enabled": true }, "Cricbuzz": { "AutoResolve": true } }
```

Our match ids are CricketData GUIDs and the source uses Cricbuzz's integers, so something has to pair them, and there is no way to use the source without pairing. See [D-020](docs/decisions.md) and [D-031](docs/decisions.md).

## Running locally

Run the backend and frontend in two terminals.

### Backend

```bash
cd backend
dotnet run --project src/CricketLive.Api
```

```text
API       http://localhost:5140
Swagger   http://localhost:5140/swagger
Health    http://localhost:5140/api/health
```

### Frontend

```bash
cd frontend
npm install
npm run dev
```

```text
App   http://localhost:5173
```

The frontend reads the API base URL from `VITE_API_BASE_URL`. See `frontend/.env.example`; `frontend/.env.development` already points at the local API.

## Checks

```bash
# frontend
npm run lint
npm run typecheck
npm run format:check
npm run build

# backend
dotnet build
dotnet test
```
