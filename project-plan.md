# 🏏 Cricket Live

A modern, responsive, real-time cricket score web application inspired by platforms such as Cricbuzz and CREX.

The application will provide live cricket scores, match details, scorecards, commentary, series information, teams, players, and other cricket-related information through a responsive web experience.

The project is designed to start as a **free/low-cost MVP** while maintaining an architecture that can later scale to a production-grade application.

---

# 📋 Table of Contents

* [Project Overview](#-project-overview)
* [Goals](#-goals)
* [Core Features](#-core-features)
* [Technology Stack](#-technology-stack)
* [System Architecture](#-system-architecture)
* [Live Score Architecture](#-live-score-architecture)
* [Data Provider](#-data-provider)
* [Redis Strategy](#-redis-strategy)
* [Frontend Architecture](#-frontend-architecture)
* [Backend Architecture](#-backend-architecture)
* [Database Strategy](#-database-strategy)
* [Project Structure](#-project-structure)
* [Application Pages](#-application-pages)
* [Responsive Design](#-responsive-design)
* [API Design](#-api-design)
* [Development Roadmap](#-development-roadmap)
* [Sprint Plan](#-sprint-plan)
* [Testing Strategy](#-testing-strategy)
* [Security](#-security)
* [Performance](#-performance)
* [Error Handling](#-error-handling)
* [Logging and Monitoring](#-logging-and-monitoring)
* [Deployment](#-deployment)
* [Environment Variables](#-environment-variables)
* [Git Strategy](#-git-strategy)
* [Development Rules](#-development-rules)
* [Future Enhancements](#-future-enhancements)
* [Project Status](#-project-status)

---

# 📌 Project Overview

**Cricket Live** is a real-time cricket information platform.

The application will consume cricket data from an external cricket data provider and expose normalized data through our own ASP.NET Core backend.

The frontend will be built using React, TypeScript, Vite, and Tailwind CSS.

Live match updates will be delivered to connected clients using **Server-Sent Events (SSE)**.

Redis will be used as the live-data/cache layer so that the external cricket provider is not called separately for every user.

## High-Level Flow

```text
                    Cricket Data Provider
                           │
                           ▼
                  ASP.NET Core Backend
                           │
                    Background Service
                           │
                           ▼
                         Redis
                      /          \
                     /            \
                    ▼              ▼
              REST API            SSE
                    \              /
                     \            /
                      ▼          ▼
                         React
                           │
                           ▼
                         User
```

---

# 🎯 Goals

## Primary Goals

* Build a modern cricket score application.
* Display live cricket matches.
* Provide near-real-time score updates.
* Provide detailed match information.
* Build a completely responsive UI.
* Use a clean and scalable architecture.
* Keep the initial infrastructure free/low-cost.
* Avoid direct dependency on the external cricket API from the frontend.
* Keep the cricket data provider replaceable.
* Follow production-level development practices.

## Secondary Goals

* Build a reusable cricket-data abstraction.
* Implement efficient caching.
* Minimize external API requests.
* Support thousands of connected clients through efficient live updates.
* Keep the frontend optimized for mobile devices.
* Make the project suitable as a portfolio/full-stack project.

---

# 🏏 Core Features

## MVP Features

### Home

* Featured live match
* Live matches
* Upcoming matches
* Recent results
* Popular competitions
* Quick navigation

### Live Matches

* Live match list
* Match status
* Current score
* Overs
* Teams
* Match result/status

### Match Details

* Match header
* Teams
* Current score
* Match status
* Current batsmen
* Current bowler
* Summary
* Scorecard
* Commentary/timeline
* Statistics where supported by the provider

### Series

* Series information
* Fixtures
* Results
* Points table
* Participating teams

### Teams

* Team information
* Players
* Matches
* Recent results
* Upcoming matches

### Players

* Player information
* Batting statistics
* Bowling statistics
* Recent matches

### Search

Search for:

* Matches
* Teams
* Players
* Series

---

# 🧰 Technology Stack

## Frontend

| Technology     | Purpose       |
| -------------- | ------------- |
| React          | UI framework  |
| TypeScript     | Type safety   |
| Vite           | Build tooling |
| Tailwind CSS   | Styling       |
| React Router   | Routing       |
| TanStack Query | Server state  |
| Zustand        | Client state  |
| Lucide React   | Icons         |

---

## Backend

| Technology            | Purpose             |
| --------------------- | ------------------- |
| ASP.NET Core          | REST API            |
| C#                    | Backend language    |
| Entity Framework Core | ORM                 |
| PostgreSQL            | Persistent database |
| Redis                 | Cache/live state    |
| SSE                   | Live updates        |
| BackgroundService     | Provider polling    |
| xUnit                 | Backend testing     |

---

## External Services

| Service                    | Purpose          |
| -------------------------- | ---------------- |
| SportScore                 | Cricket data     |
| GitHub                     | Source control   |
| Vercel/Netlify             | Frontend hosting |
| Free .NET hosting provider | Backend hosting  |
| PostgreSQL provider        | Database         |
| Redis provider             | Cache            |

---

# 🏗 System Architecture

```text
                                  ┌─────────────────────┐
                                  │    SportScore API   │
                                  │   Cricket Provider   │
                                  └──────────┬──────────┘
                                             │
                                             │
                                  ┌──────────▼──────────┐
                                  │ ASP.NET Core Backend│
                                  │                     │
                                  │ Cricket Provider    │
                                  │ Service             │
                                  └──────────┬──────────┘
                                             │
                                  ┌──────────▼──────────┐
                                  │ Background Service  │
                                  │                     │
                                  │ Poll live matches  │
                                  │ Detect changes      │
                                  └──────────┬──────────┘
                                             │
                                             ▼
                                      ┌─────────────┐
                                      │    Redis    │
                                      │             │
                                      │ Live State  │
                                      │ Cache       │
                                      └──────┬──────┘
                                             │
                              ┌──────────────┴─────────────┐
                              │                            │
                              ▼                            ▼
                       REST API                       SSE Stream
                              │                            │
                              └──────────────┬─────────────┘
                                             │
                                             ▼
                                  ┌─────────────────────┐
                                  │       React         │
                                  │                     │
                                  │ React + TypeScript  │
                                  │ Tailwind CSS        │
                                  └─────────────────────┘
```

---

# ⚡ Live Score Architecture

Real-time score delivery is one of the most important parts of the system.

We should **not** allow every browser to directly call SportScore.

## ❌ Incorrect Architecture

```text
User 1 ──────┐
User 2 ──────┤
User 3 ──────┤
User 4 ──────┤──── SportScore
User 5 ──────┘
```

If 1,000 users watch the same match, this could result in unnecessary external API requests.

---

## ✅ Recommended Architecture

```text
                         SportScore
                             │
                             ▼
                   .NET Background Service
                             │
                             ▼
                           Redis
                             │
                         SSE
                             │
              ┌──────────────┼──────────────┐
              ▼              ▼              ▼
            User 1         User 2         User 3
```

The external provider is queried by our backend.

The latest match state is stored in Redis.

Connected clients receive updates through SSE.

---

# 📡 Server-Sent Events

The backend will expose:

```http
GET /api/matches/{matchId}/stream
```

Example:

```text
Client
  │
  │ HTTP connection
  ▼
.NET API
  │
  │ SSE
  ▼
Live updates
```

Example event:

```json
{
  "matchId": "123",
  "status": "LIVE",
  "score": {
    "runs": 184,
    "wickets": 4,
    "overs": "18.2"
  },
  "updatedAt": "2026-09-28T18:30:00Z"
}
```

React will update the UI without refreshing the page.

---

# 🔴 Redis Strategy

Redis is part of the target architecture.

However, it does not need to be introduced during the first development sprint.

We can initially develop:

```text
SportScore
    ↓
.NET
    ↓
React
```

Then introduce:

```text
SportScore
    ↓
.NET BackgroundService
    ↓
Redis
    ↓
SSE
    ↓
React
```

## Redis Responsibilities

Redis will be used for:

* Live match state
* Current score
* Current over
* Current batsmen
* Current bowler
* Recent commentary
* Frequently accessed match data
* Temporary cache
* Shared state between backend instances

## Example Redis Keys

```text
live:matches

live:match:{matchId}

live:match:{matchId}:score

live:match:{matchId}:commentary

live:match:{matchId}:scorecard
```

---

# 🗄 Database Strategy

PostgreSQL will be used as the persistent database.

Redis is **not** our primary database.

## PostgreSQL

Stores:

```text
Users
Teams
Players
Competitions
Matches
Venues
Favourite Teams
Favourite Matches
Application Settings
```

## Redis

Stores:

```text
Current live score
Live match state
Temporary data
Cache
SSE-related state
```

## External Provider

Provides:

```text
Current cricket information
```

---

# 🔌 Data Provider Architecture

The application should never become tightly coupled to SportScore.

We will create an abstraction.

```csharp
public interface ICricketDataProvider
{
    Task<IReadOnlyList<Match>> GetLiveMatchesAsync(
        CancellationToken cancellationToken);

    Task<MatchDetails?> GetMatchDetailsAsync(
        string matchId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Match>> GetUpcomingMatchesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Match>> GetRecentMatchesAsync(
        CancellationToken cancellationToken);
}
```

Implementation:

```text
ICricketDataProvider
        │
        ▼
SportScoreProvider
```

Future providers can be added:

```text
SportScoreProvider
AnotherCricketProvider
```

without changing the frontend.

---

# 🌐 SportScore

SportScore will initially be used as the external cricket-data provider.

The provider currently documents support for cricket match data, live/recent matches, match details, standings, teams, players and related information.

The free API currently advertises approximately 10,000 requests per 24 hours per IP and requires visible SportScore attribution.

Provider documentation:

https://sportscore.com/developers/api/

Before any commercial launch, the provider's current usage terms and commercial licensing requirements must be verified.

---

# ⚛️ Frontend Architecture

The frontend will follow a feature-oriented architecture.

```text
src/
│
├── app/
│   ├── App.tsx
│   ├── router.tsx
│   └── providers.tsx
│
├── components/
│   ├── common/
│   ├── layout/
│   ├── match/
│   ├── team/
│   └── player/
│
├── features/
│   ├── matches/
│   │   ├── api/
│   │   ├── components/
│   │   ├── hooks/
│   │   ├── types/
│   │   └── utils/
│   │
│   ├── live-score/
│   ├── teams/
│   ├── players/
│   └── competitions/
│
├── pages/
│   ├── Home/
│   ├── Live/
│   ├── MatchDetails/
│   ├── Series/
│   ├── Team/
│   └── Player/
│
├── hooks/
├── services/
├── store/
├── types/
├── utils/
├── styles/
│
└── main.tsx
```

---

# 🧩 Reusable Components

Components should be reusable and independent.

## Common

```text
Button
Badge
Card
Skeleton
Spinner
EmptyState
ErrorState
Modal
Tabs
Dropdown
```

## Match

```text
MatchCard
MatchHeader
MatchScore
MatchStatus
TeamScore
Scorecard
BattingTable
BowlingTable
Commentary
MatchTabs
```

## Team

```text
TeamCard
TeamHeader
TeamPlayers
TeamMatches
```

## Player

```text
PlayerCard
PlayerHeader
PlayerStats
```

---

# 📱 Responsive Design

The application will be designed **mobile-first**.

Target devices:

```text
Mobile
Tablet
Laptop
Desktop
Large Desktop
```

Tailwind breakpoints:

```text
default → Mobile

sm      → Large Mobile
md      → Tablet
lg      → Laptop
xl      → Desktop
2xl     → Large Desktop
```

Example:

```tsx
<div className="
  grid
  grid-cols-1
  sm:grid-cols-2
  lg:grid-cols-3
  xl:grid-cols-4
  gap-4
">
```

---

# 🎨 UI Principles

The application should have:

* Clean cricket-focused interface
* Clear score hierarchy
* Strong live-status indicators
* Good spacing
* Readable typography
* Touch-friendly controls
* Minimal unnecessary scrolling
* No accidental horizontal scrolling
* Responsive tables
* Skeleton loading
* Proper empty states
* Proper error states
* Accessible contrast

The UI should not simply copy Cricbuzz or CREX.

We will use them as **product references**, while maintaining our own visual identity.

---

# 📄 Application Pages

## `/`

Home page.

Sections:

```text
Featured Match
Live Matches
Upcoming Matches
Recent Results
Popular Series
```

---

## `/live`

Live matches.

```text
Live Now
```

---

## `/matches`

All matches.

Filters:

```text
Live
Upcoming
Completed
Date
Series
```

---

## `/match/:slug`

Match details.

Tabs:

```text
Summary
Scorecard
Commentary
Stats
```

---

## `/series/:slug`

Series details.

```text
Overview
Matches
Points Table
Teams
Results
```

---

## `/team/:slug`

Team details.

```text
Overview
Players
Matches
Statistics
```

---

## `/player/:slug`

Player details.

```text
Profile
Batting
Bowling
Recent Matches
Statistics
```

---

# 🔗 Backend API

All frontend API communication should go through our .NET API.

## Matches

```http
GET /api/matches/live
GET /api/matches/upcoming
GET /api/matches/recent
GET /api/matches/{matchId}
```

## Match Data

```http
GET /api/matches/{matchId}/scorecard
GET /api/matches/{matchId}/commentary
GET /api/matches/{matchId}/stats
```

## Live Stream

```http
GET /api/matches/{matchId}/stream
```

## Series

```http
GET /api/series
GET /api/series/{seriesId}
GET /api/series/{seriesId}/matches
GET /api/series/{seriesId}/standings
```

## Teams

```http
GET /api/teams
GET /api/teams/{teamId}
GET /api/teams/{teamId}/matches
GET /api/teams/{teamId}/players
```

## Players

```http
GET /api/players
GET /api/players/{playerId}
GET /api/players/{playerId}/stats
```

---

# 📦 Standard API Response

Backend responses should follow a consistent structure.

```json
{
  "success": true,
  "data": {},
  "message": "Success"
}
```

For errors:

```json
{
  "success": false,
  "data": null,
  "message": "Unable to retrieve match details"
}
```

For validation errors:

```json
{
  "success": false,
  "data": null,
  "message": "Invalid match identifier",
  "errors": []
}
```

---

# 🏃 Development Roadmap

The project will be developed in controlled sprints.

```text
Sprint 1
Foundation
    ↓
Sprint 2
UI Foundation
    ↓
Sprint 3
SportScore Integration
    ↓
Sprint 4
Home + Match Pages
    ↓
Sprint 5
Live Engine
    ↓
Sprint 6
Scorecard + Commentary
    ↓
Sprint 7
Series + Teams + Players
    ↓
Sprint 8
Production Hardening
    ↓
Deployment
```

---

# 🚀 Sprint 1 — Project Foundation

## Goal

Create the complete development environment.

### Frontend

* Initialize React + Vite + TypeScript
* Configure Tailwind CSS
* Configure ESLint
* Configure Prettier
* Configure React Router
* Configure TanStack Query
* Configure Zustand
* Configure environment variables

### Backend

* Create ASP.NET Core Web API
* Configure dependency injection
* Configure Swagger/OpenAPI
* Configure CORS
* Configure global exception handling
* Configure logging
* Create basic health endpoint

### Repository

```text
frontend/
backend/
docs/
README.md
.gitignore
```

### Completion Criteria

```text
[ ] React application runs
[ ] .NET API runs
[ ] React can call .NET
[ ] Swagger works
[ ] Tailwind works
[ ] Routing works
[ ] Git repository configured
```

---

# 🎨 Sprint 2 — UI Foundation

## Goal

Build the reusable design system.

### Components

```text
Header
Footer
Navigation
Container
Button
Card
Badge
Skeleton
Spinner
ErrorState
EmptyState
Tabs
```

### Pages

Create initial mocked pages:

```text
Home
Live
Match Details
```

No external cricket API yet.

### Completion Criteria

```text
[ ] Mobile UI
[ ] Tablet UI
[ ] Desktop UI
[ ] No horizontal overflow
[ ] Reusable components
[ ] Loading states
[ ] Empty states
[ ] Error states
```

---

# 🔌 Sprint 3 — SportScore Integration

## Goal

Connect real cricket data.

Implement:

```text
SportScoreProvider
ICricketDataProvider
Match DTOs
Series DTOs
Team DTOs
Player DTOs
```

Backend:

```text
/api/matches/live
/api/matches/upcoming
/api/matches/recent
/api/matches/{id}
```

### Important

Do not expose raw SportScore responses directly to React.

Instead:

```text
SportScore Response
        ↓
Provider
        ↓
Mapper
        ↓
Application DTO
        ↓
React
```

This protects our frontend from external API changes.

---

# 🏠 Sprint 4 — Home + Match Experience

Build:

```text
Home
Live Matches
Match Details
```

Match details:

```text
Match Header
Score
Teams
Status
Current batsmen
Current bowler
Summary
```

Responsive behavior must be implemented during development rather than postponed.

---

# ⚡ Sprint 5 — Live Engine

## Goal

Implement live score updates.

### Backend

Create:

```text
LiveMatchBackgroundService
LiveMatchService
RedisService
SseService
```

Architecture:

```text
SportScore
     ↓
BackgroundService
     ↓
Redis
     ↓
SSE
     ↓
React
```

### Frontend

Create:

```text
useMatchLiveStream()
```

Handle:

```text
Connected
Message received
Disconnected
Reconnect
Error
```

### Completion Criteria

```text
[ ] Live match opens
[ ] SSE connection established
[ ] Score update received
[ ] UI updates automatically
[ ] Browser refresh not required
[ ] Connection reconnects after failure
```

---

# 📊 Sprint 6 — Scorecard + Commentary

Implement:

```text
Batting
Bowling
Partnership
Fall of Wickets
Overs
Commentary
Match Timeline
```

Only display information actually available from the external provider.

---

# 🌍 Sprint 7 — Cricket Ecosystem

Implement:

```text
Series
Teams
Players
Standings
Search
Fixtures
Results
```

Add navigation between entities.

Example:

```text
Match
 ↓
Team
 ↓
Player
 ↓
Statistics
```

---

# 🛡 Sprint 8 — Production Hardening

## Frontend

```text
Lazy loading
Code splitting
Error boundaries
SEO metadata
Accessibility
Image optimization
Performance optimization
Bundle analysis
```

## Backend

```text
Global exception handling
Rate limiting
Caching
Retry policies
Timeouts
CancellationToken
Health checks
Structured logging
```

## Security

```text
CORS
Security headers
Input validation
Secret management
API abuse protection
```

---

# 🧪 Testing Strategy

Testing will be introduced throughout development.

## Frontend

Test:

```text
Components
Hooks
API services
State
Live score updates
Responsive behavior
```

## Backend

Test:

```text
Controllers
Services
Provider integration
DTO mapping
Redis integration
SSE behavior
Error handling
```

## Integration Tests

Important flows:

```text
SportScore
   ↓
.NET
   ↓
Redis
   ↓
SSE
   ↓
React
```

---

# 🔐 Security

Security requirements:

* Never expose provider secrets in React.
* Never commit API keys/secrets.
* Use environment variables.
* Configure CORS explicitly.
* Validate external API responses.
* Validate route parameters.
* Add rate limiting.
* Add request timeouts.
* Use HTTPS in production.
* Avoid sensitive data in logs.
* Sanitize externally supplied text before rendering.
* Apply security headers.

---

# 🚦 Error Handling

The application must gracefully handle:

### Provider unavailable

```text
SportScore unavailable
       ↓
Use cached data if available
       ↓
Display stale-data indicator
```

### Redis unavailable

```text
Redis unavailable
       ↓
Log error
       ↓
Fallback where possible
       ↓
Do not crash entire API
```

### SSE disconnect

```text
Connection lost
       ↓
Client reconnect
       ↓
Fetch latest match state
       ↓
Resume live stream
```

### No matches

Show:

```text
No live matches currently.
```

instead of an empty page.

---

# ⚡ Performance Strategy

## Frontend

* Lazy-load routes.
* Use TanStack Query caching.
* Avoid unnecessary re-renders.
* Memoize only where useful.
* Optimize images.
* Use skeleton states.
* Avoid unnecessary global state.
* Avoid large client-side data processing.

## Backend

* Cache frequently accessed data.
* Avoid unnecessary provider calls.
* Use asynchronous APIs.
* Use cancellation tokens.
* Configure HTTP client timeouts.
* Use connection pooling.
* Use Redis for live data.

## API

External provider requests should be minimized.

```text
Bad:

100 users
↓
100 provider requests
```

Preferred:

```text
Provider
↓
Backend
↓
Redis
↓
100 clients
```

---

# 📈 Scaling Strategy

Initial:

```text
One frontend
One backend
One Redis
One database
```

Later:

```text
              Load Balancer
                    │
        ┌───────────┼───────────┐
        ▼           ▼           ▼
      API 1       API 2       API 3
        │           │           │
        └───────────┼───────────┘
                    ▼
                  Redis
                    │
                 Database
```

Redis allows multiple backend instances to share live state.

---

# 📝 Logging

Backend logging categories:

```text
API Request
Provider Request
Provider Response Error
Redis Error
SSE Connection
Background Service
Database Error
Unexpected Exception
```

Example:

```text
[INFO] Live match polling started
[INFO] Match 123 updated
[WARN] SportScore request timed out
[INFO] SSE client connected
[INFO] SSE client disconnected
```

Do not log:

```text
API keys
Passwords
Tokens
Sensitive user information
```

---

# ❤️ Health Checks

Backend should expose:

```http
GET /health
```

Potential checks:

```text
API
Database
Redis
External Provider
```

Example:

```json
{
  "status": "Healthy"
}
```

---

# 🌐 Deployment

## Frontend

Preferred initial hosting:

```text
Vercel
```

Alternative:

```text
Netlify
```

## Backend

Use a free/low-cost .NET-capable hosting platform during MVP development.

The exact provider should be selected based on:

* .NET support
* Long-lived SSE connections
* Free-tier limits
* Sleep behavior
* Deployment method
* Region
* Bandwidth

## Database

Use a free PostgreSQL provider during MVP development.

## Redis

Use a free Redis provider during MVP development.

---

# 🔑 Environment Variables

## React

```env
VITE_API_BASE_URL=https://api.example.com
```

## .NET

```env
ConnectionStrings__DefaultConnection=

Redis__ConnectionString=

SportScore__BaseUrl=
```

Never commit:

```text
.env
.env.local
production secrets
API keys
connection strings
```

---

# 🔀 Git Strategy

Recommended branches:

```text
main
develop
feature/*
bugfix/*
hotfix/*
```

Example:

```text
feature/live-score
feature/match-details
feature/scorecard
bugfix/sse-reconnect
```

## Commit examples

```text
feat: add live matches endpoint
feat: implement match details page
feat: add SSE live score stream
feat: add redis live match cache
fix: handle SSE reconnect
fix: prevent match card overflow
refactor: extract cricket provider
```

---

# 📌 Pull Request Rules

Every PR should contain:

```text
What changed?
Why was it changed?
How was it tested?
Screenshots if UI changed
Any known limitations?
```

Before merging:

```text
[ ] Build passes
[ ] Tests pass
[ ] No TypeScript errors
[ ] No lint errors
[ ] Responsive UI verified
[ ] No unnecessary console logs
[ ] No secrets committed
```

---

# 🧱 Development Rules

## 1. Do not over-engineer

Implement only what is required for the current sprint.

---

## 2. No direct external API calls from React

Use:

```text
React
 ↓
.NET
 ↓
SportScore
```

not:

```text
React
 ↓
SportScore
```

---

## 3. Keep provider-specific code isolated

All SportScore-specific logic should remain inside:

```text
Infrastructure/SportScore
```

---

## 4. Do not expose external response models

Use our own DTOs.

```text
SportScore Model
      ↓
Mapper
      ↓
Application DTO
```

---

## 5. Mobile-first

Every UI component must work on:

```text
Mobile
Tablet
Desktop
```

---

## 6. No unnecessary scrolling

Avoid:

* Unnecessary page scroll containers
* Nested scrollbars
* Horizontal overflow
* Fixed-height content that clips information

Tables should become responsive instead of breaking the page layout.

---

## 7. Accessibility

Use:

```text
Semantic HTML
Keyboard navigation
ARIA where necessary
Accessible labels
Visible focus states
Sufficient contrast
```

---

## 8. TypeScript

Avoid:

```typescript
any
```

Prefer:

```typescript
unknown
```

with proper narrowing when the type is genuinely unknown.

---

## 9. React

Prefer:

```text
Functional components
Custom hooks
Composition
Feature-based organization
```

Avoid unnecessary:

```text
Huge components
Global state
useEffect for derived state
Prop drilling across many layers
```

---

## 10. Backend

Use:

```text
Dependency Injection
Async/Await
CancellationToken
DTOs
Interfaces
Structured logging
```

Avoid:

```text
Business logic inside controllers
Huge services
Static global state
Blocking calls
```

---

# 📊 Project Milestones

## Milestone 1

### Foundation

```text
React
.NET
Tailwind
Routing
Git
CI
```

Status:

```text
⬜ Not Started
```

---

## Milestone 2

### UI Foundation

```text
Header
Navigation
Cards
Responsive layout
Skeletons
```

Status:

```text
⬜ Not Started
```

---

## Milestone 3

### Cricket API

```text
SportScore
Provider abstraction
DTOs
Match APIs
```

Status:

```text
⬜ Not Started
```

---

## Milestone 4

### Match Experience

```text
Home
Live
Match Details
```

Status:

```text
⬜ Not Started
```

---

## Milestone 5

### Real-Time Engine

```text
BackgroundService
Redis
SSE
React live updates
```

Status:

```text
⬜ Not Started
```

---

## Milestone 6

### Cricket Details

```text
Scorecard
Commentary
Stats
```

Status:

```text
⬜ Not Started
```

---

## Milestone 7

### Cricket Ecosystem

```text
Series
Teams
Players
Search
Standings
```

Status:

```text
⬜ Not Started
```

---

## Milestone 8

### Production

```text
Testing
Security
Performance
Monitoring
Deployment
```

Status:

```text
⬜ Not Started
```

---

# 🚀 Future Enhancements

After the MVP is stable:

## User Features

```text
User accounts
Favourite teams
Favourite players
Favourite matches
Personalized home
```

## Notifications

```text
Match started
Wicket
Fifty
Century
Match result
Innings break
```

## PWA

```text
Installable application
Offline shell
Push notifications
```

## Advanced Statistics

```text
Batting trends
Bowling trends
Team performance
Head-to-head
Player comparisons
Venue statistics
```

## Admin

```text
Dashboard
Provider monitoring
API usage
Live match monitoring
Error monitoring
System health
```

---

# 💰 Cost Strategy

The initial MVP should target **₹0 infrastructure cost** where the selected providers' free tiers permit it.

Target:

```text
React Hosting       → Free
.NET Hosting        → Free tier
PostgreSQL          → Free tier
Redis               → Free tier
GitHub              → Free
SportScore          → Free tier
```

A custom domain is optional and may cost money.

Free hosting should be treated as an **MVP/portfolio strategy**, not a guarantee of unlimited production capacity.

---

# ⚠️ External API Considerations

SportScore's free tier and terms may change.

Before production/commercial usage, verify:

* Request limits
* Cricket coverage
* Data freshness
* Attribution requirements
* Commercial usage
* Redistribution rights
* API availability
* SLA
* Rate limits

The application should therefore keep the provider behind:

```text
ICricketDataProvider
```

so another provider can be introduced if required.

---

# 🧠 Important Architectural Decision

The application will **not assume that the external provider is truly sub-second real-time**.

The provider's actual update frequency determines how frequently our application can receive new cricket data.

Therefore:

```text
Provider Update
       ↓
Detect Change
       ↓
Update Redis
       ↓
Broadcast SSE
```

rather than pretending that our system can generate new cricket information independently.

---

# 📦 Final Architecture

```text
                         ┌──────────────────────┐
                         │      SportScore      │
                         │   Cricket Provider   │
                         └──────────┬───────────┘
                                    │
                                    ▼
                         ┌──────────────────────┐
                         │  ASP.NET Core API    │
                         │                      │
                         │ Cricket Provider     │
                         │ Application Services │
                         │ Controllers          │
                         └──────────┬───────────┘
                                    │
                                    ▼
                         ┌──────────────────────┐
                         │ Background Service   │
                         │                      │
                         │ Live Match Polling   │
                         │ Change Detection     │
                         └──────────┬───────────┘
                                    │
                                    ▼
                         ┌──────────────────────┐
                         │        Redis         │
                         │                      │
                         │ Live State           │
                         │ Cache                │
                         └──────────┬───────────┘
                                    │
                         ┌──────────┴──────────┐
                         │                     │
                         ▼                     ▼
                    REST API                 SSE
                         │                     │
                         └──────────┬──────────┘
                                    │
                                    ▼
                         ┌──────────────────────┐
                         │        React        │
                         │                      │
                         │ TypeScript           │
                         │ Vite                 │
                         │ Tailwind CSS         │
                         │ TanStack Query       │
                         │ Zustand              │
                         └──────────────────────┘
```

---

# 🏁 Definition of Done

A feature is considered complete only when:

```text
[ ] Requirement implemented
[ ] TypeScript/C# types are correct
[ ] Responsive UI completed
[ ] Loading state handled
[ ] Empty state handled
[ ] Error state handled
[ ] API errors handled
[ ] Accessibility considered
[ ] Tests added where appropriate
[ ] No console errors
[ ] No unnecessary logs
[ ] No secrets committed
[ ] Build succeeds
[ ] Lint succeeds
[ ] Existing functionality still works
```

---

# 📍 Current Project Status

## Phase

**Planning**

## Current Milestone

**Sprint 1 — Foundation**

## Status

```text
⬜ Project initialization
⬜ React + Vite setup
⬜ TypeScript setup
⬜ Tailwind setup
⬜ React Router setup
⬜ TanStack Query setup
⬜ Zustand setup
⬜ .NET API setup
⬜ PostgreSQL setup
⬜ Redis setup
⬜ CI setup
```

---

# 🎯 First Development Target

The first working vertical slice should be:

```text
React
   ↓
.NET API
   ↓
Mock Match Data
   ↓
Match Card
   ↓
Responsive Home Page
```

After this is stable:

```text
React
   ↓
.NET API
   ↓
SportScore
   ↓
Real Match Data
```

Then:

```text
SportScore
   ↓
.NET BackgroundService
   ↓
Redis
   ↓
SSE
   ↓
React
```

This incremental approach prevents us from debugging the UI, external API, Redis, SSE, and deployment simultaneously.

---

# 🏏 Project Vision

The final application should feel like a **fast, modern cricket information platform**, with:

```text
Fast UI
+
Responsive Design
+
Clean Architecture
+
Real-Time Updates
+
Efficient Caching
+
Reliable API Layer
+
Production-Ready Practices
```

The first objective is not to build every feature.

The first objective is to build a **solid live-score engine and excellent match experience**, then expand the platform around it.
