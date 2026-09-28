# 🏏 Cricket Live

A modern, responsive real-time cricket score platform built with React, TypeScript, Tailwind CSS, and ASP.NET Core, featuring live scores, match details, scorecards, commentary, and SSE-powered updates.

## Documentation

* [Project plan](project-plan.md) — architecture and requirements
* [Sprint plan](docs/sprint-plan.md) — what gets built, in what order

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
