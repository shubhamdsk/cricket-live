# 🗓 Cricket Live — Sprint Execution Plan

This document turns the sprints defined in [`project-plan.md`](../project-plan.md) into executable work items.

`project-plan.md` remains the source of truth for **architecture and requirements**.
This document is the source of truth for **what we build, in what order, and when a sprint is finished**.
[`engineering-standards.md`](./engineering-standards.md) is the source of truth for **how** — it is binding for every sprint listed below.

---

## Assumptions

```text
Team size        → 1 developer
Sprint length    → 1 week
Estimates        → working days
Branching        → feature branches into develop (see Git Strategy)
```

Estimates are effort, not calendar time. Adjust the sprint length rather than the ordering — the ordering encodes real technical dependencies.

---

## Sprint Overview

| Sprint | Name                  | Outcome                                        | Est. | Depends on |
| ------ | --------------------- | ---------------------------------------------- | ---- | ---------- |
| 1      | Foundation            | React and .NET run and talk to each other      | 5d   | —          |
| 2      | UI Foundation         | Design system + mocked pages                   | 5d   | 1          |
| 3      | Cricket Data Integration | Real cricket data behind our own DTOs       | 5d   | 1          |
| 4      | Home + Match          | Real data rendered in real pages               | 5d   | 2, 3       |
| 5      | Live Engine           | Scores update without refresh                  | 6d   | 4          |
| 6      | Scorecard + Commentary| Deep match detail                              | 5d   | 4          |
| 7      | Cricket Ecosystem     | Series, teams, players, search                 | 6d   | 4          |
| 8      | Production Hardening  | Deployed, observable, secured                  | 6d   | 5, 6, 7    |

Sprints 2 and 3 are independent of each other and can be reordered or interleaved. Everything from Sprint 4 onward is strictly sequential on its dependencies.

---

## Vertical Slice Principle

Per the project plan's First Development Target, each sprint must end with something runnable end to end. We never build a layer in isolation.

```text
Sprint 1   React → .NET → health endpoint
Sprint 2   React → mock data → responsive UI
Sprint 3   .NET → CricketData → our DTOs
Sprint 4   React → .NET → CricketData → Home + Match
Sprint 5   CricketData → BackgroundService → Redis → SSE → React
```

---

# 🚀 Sprint 1 — Foundation

**Goal:** a working development environment where React can call .NET.

### Repository

* [ ] `1.1` Create folder structure: `frontend/`, `backend/`, `docs/`
* [ ] `1.2` Add root `.gitignore` covering Node, .NET, IDE, and env files
* [ ] `1.3` Expand `README.md` with setup and run instructions
* [ ] `1.4` Create `develop` branch and set branch protection expectations

### Frontend

* [ ] `1.5` Initialize React + TypeScript via Vite in `frontend/`
* [ ] `1.6` Configure Tailwind CSS with the mobile-first breakpoints from the plan
* [ ] `1.7` Configure ESLint and Prettier, wire `lint` and `format` scripts
* [ ] `1.8` Set up React Router with placeholder routes for `/`, `/live`, `/matches`, `/match/:slug`
* [ ] `1.9` Set up TanStack Query provider in `app/providers.tsx`
* [ ] `1.10` Set up Zustand store skeleton in `store/`
* [ ] `1.11` Create typed API client in `services/` reading `VITE_API_BASE_URL`
* [ ] `1.12` Add `.env.example`

### Backend

* [ ] `1.13` Create ASP.NET Core Web API in `backend/`
* [ ] `1.14` Establish layered projects: `Api`, `Application`, `Domain`, `Infrastructure`
* [ ] `1.15` Configure Swagger/OpenAPI
* [ ] `1.16` Configure CORS for the Vite dev origin
* [ ] `1.17` Add global exception handling middleware returning the standard API response shape
* [ ] `1.18` Add the `ApiResponse<T>` envelope (`success`, `data`, `message`, `errors`)
* [ ] `1.19` Configure structured logging
* [ ] `1.20` Add `GET /api/health`
* [ ] `1.21` Add `appsettings.Development.json` and `.env.example` equivalents, no secrets committed

### CI

* [ ] `1.22` GitHub Actions workflow: frontend install, lint, build
* [ ] `1.23` GitHub Actions workflow: backend restore, build, test

### Exit criteria

```text
[ ] React application runs
[ ] .NET API runs
[ ] React successfully calls /api/health and renders the result
[ ] Swagger works
[ ] Tailwind classes apply
[ ] Routing works
[ ] CI is green on develop
```

**Not in this sprint:** PostgreSQL, Redis, the cricket provider, real UI design.

> The plan's Sprint 1 lists PostgreSQL and Redis setup. Both are deferred: Redis to Sprint 5 where it is first needed, PostgreSQL to Sprint 7 where persisted entities first appear. Standing up infrastructure we do not yet read from adds failure modes without adding value.

---

# 🎨 Sprint 2 — UI Foundation

**Goal:** a reusable design system and mocked pages, with no cricket API involved.

### Design system

* [ ] `2.1` Define Tailwind theme: colors, typography scale, spacing, live-status accent
* [ ] `2.2` Layout components: `Header`, `Footer`, `Navigation`, `Container`
* [ ] `2.3` Common components: `Button`, `Card`, `Badge`, `Tabs`
* [ ] `2.4` State components: `Skeleton`, `Spinner`, `EmptyState`, `ErrorState`
* [ ] `2.5` Match components against mock data: `MatchCard`, `MatchStatus`, `TeamScore`

### Pages

* [ ] `2.6` Home with Featured, Live, Upcoming, Recent, Popular Series sections
* [ ] `2.7` Live matches page
* [ ] `2.8` Match details shell with Summary / Scorecard / Commentary / Stats tabs
* [ ] `2.9` Mock data fixtures typed with the DTO shapes Sprint 3 will produce

### Quality

* [ ] `2.10` Verify every page at mobile, tablet, laptop, desktop widths
* [ ] `2.11` Keyboard navigation and visible focus states
* [ ] `2.12` Loading, empty, and error states rendered for each section

### Exit criteria

```text
[ ] Mobile, tablet, and desktop layouts verified
[ ] No horizontal overflow at any breakpoint
[ ] No nested scroll containers
[ ] Components are reusable and independent
[ ] Loading, empty, and error states exist
[ ] Contrast and focus states pass a manual accessibility check
```

Agreeing the mock data shape in `2.9` is what lets Sprint 4 be a swap rather than a rewrite.

---

# 🔌 Sprint 3 — Cricket Data Integration

**Goal:** real cricket data reaching our API, normalized into our own DTOs.

> Renamed from "SportScore Integration". The spike rejected SportScore and chose CricketData — see [D-012](./decisions.md).

### Provider spike (do this first)

* [x] `3.1` Obtain an API key and confirm the free-tier request limit
* [x] `3.2` Capture real cricket responses for live, upcoming, recent, and match detail
* [x] `3.3` Document which fields actually exist: scorecard, commentary, batsmen, bowler, standings
* [x] `3.4` Measure live update frequency — this sets the Sprint 5 polling interval
* [x] `3.5` Record attribution requirements in `docs/`

`3.3` and `3.4` are gating. Sprints 5 and 6 are scoped from their findings, since we can only display what the provider returns.

### Abstraction

* [x] `3.6` Define `ICricketDataProvider` in `Application`
* [x] `3.7` Implement `CricketDataProvider` in `Infrastructure/CricketData`
* [x] `3.8` Typed `HttpClient` with base URL, credential, and timeout
* [x] `3.9` Provider response models, kept internal to `Infrastructure`
* [x] `3.10` Mappers from provider models to application DTOs
* [x] `3.11` Application DTOs: `MatchDto`, `MatchDetailsDto`, `TeamDto`, `TeamInningsDto`, `InningsScoreDto`

### Endpoints

* [x] `3.12` `GET /api/matches/live`
* [x] `3.13` `GET /api/matches/upcoming`
* [x] `3.14` `GET /api/matches/recent`
* [x] `3.15` `GET /api/matches/{matchId}`

### Resilience

* [x] `3.16` In-memory response caching to stay within the request budget
* [x] `3.17` Retry with backoff and timeout handling
* [x] `3.18` Provider failures map to our error envelope, never a 500 leak
* [x] `3.19` Unit tests for mappers using the captured fixtures from `3.2`

### Exit criteria

```text
[x] All four match endpoints return real cricket data
[x] No provider model is reachable from the API surface
[x] All provider code lives under Infrastructure/CricketData
[x] Provider errors produce our standard error response
[x] Mapper tests pass against captured fixtures
[x] Request volume per page view is understood and bounded
```

---

# 🏠 Sprint 4 — Home + Match Experience

**Goal:** replace mock data with the real API. The first complete vertical slice.

* [x] `4.1` Generate or hand-write frontend types matching the API DTOs
* [x] `4.2` `features/matches/api` query functions
* [x] `4.3` TanStack Query hooks: `useLiveMatches`, `useUpcomingMatches`, `useRecentMatches`, `useMatchDetails`
* [x] `4.4` Wire Home sections to real data
* [x] `4.5` Wire Live page to real data
* [x] `4.6` Match details: header, per-innings breakdown, match information
* [x] `4.7` Slug-based routing and resolution for `/match/:slug`
* [x] `4.8` Real loading skeletons, empty states, and error states on every section
* [x] `4.9` Configure query stale times and refetch behavior per data type
* [x] `4.10` Re-verify responsiveness with real, variable-length data

### Exit criteria

```text
[x] Home renders live cricket data
[x] Live page renders live cricket data
[x] Match details renders a real match
[x] No mock data remains in these paths
[x] Long team names and unusual scores do not break layout
[x] Loading, empty, and error states verified against the real API
```

**Status: ✅ Complete**

### Sprint 4 notes

`4.6` was written in Sprint 1 against a data shape we had not measured. The provider carries no toss,
no editorial summary, and no players at the crease, so per the decision recorded in D-012 those
sections were removed rather than stubbed. What replaced them is real: a per-innings breakdown,
which is the only part of the payload the header does not already show, and a match information
card. `features/matches/components/CurrentPlayers.tsx` and both mock modules are deleted.

The Summary / Scorecard / Commentary / Stats tablist went with them. Three of its four panels were
placeholders pointing at Sprint 6, and a tablist that is three-quarters empty is worse than no
tablist. `components/common/Tabs` stays in the design system for Sprint 6 to use.

Two duplications surfaced only once real data was flowing. The home hero and the live list are the
same query, so an outage printed the same error twice and a quiet day printed the same empty state
twice; the hero now renders only when there is a match to feature. And `logoUrl` was arriving
unused, so `TeamScoreRow` now shows a crest when the provider has one — it does not for domestic
sides, which is why there is no placeholder shape.

Refetch behaviour is set against what the data can do rather than how live the page should feel.
Our API caches a provider response for five minutes, so live data polls at one minute, fixtures and
results at five, and a completed match stops polling entirely. A 4xx is no longer retried; repeating
a request the API has already rejected cannot change the answer, and it was costing every unknown
slug a second round trip. Sprint 5 replaces the live interval with an SSE push.

Verified against the running API on a day with one completed match and nothing live: populated
lists, both empty states, the unreachable-API error state on all three sections, a real match detail
page, an unknown slug returning the not-found state, and the whole thing at 360 px.

Sprints 5, 6, and 7 all branch from here and can be reordered by priority. Sprint 5 is the project's differentiator and should stay next.

---

# ⚡ Sprint 5 — Live Engine

**Goal:** scores update in the browser without a refresh.

### Redis — deferred, see [D-014](./decisions.md)

* [ ] ~~`5.1` Provision Redis and add connection configuration~~
* [ ] ~~`5.2` `RedisService` wrapper with serialization~~
* [ ] ~~`5.3` Implement the key scheme: `live:matches`, `live:match:{id}`, `live:match:{id}:score`~~
* [ ] ~~`5.4` Define TTLs and a cleanup path for finished matches~~

### Background polling

* [x] `5.5` `LiveMatchPoller` polling at the interval established in `3.4`
* [x] `5.6` Change detection so unchanged state does not broadcast
* [x] `5.7` Back off polling when no matches are live, to protect the request budget
* [x] `5.8` Write detected state to the in-process broadcaster
* [x] `5.9` `CancellationToken` respected throughout

### SSE

* [x] `5.10` `MatchBroadcaster` managing connections per match
* [x] `5.11` `GET /api/matches/{matchId}/stream`
* [x] `5.12` Send current state on connect so clients are never blank
* [x] `5.13` Heartbeat to keep proxies from closing idle connections
* [x] `5.14` Clean up connections on client disconnect

### Frontend

* [x] `5.15` `useMatchLiveStream()` handling connected, message, disconnected, reconnect, error
* [x] `5.16` Reconnect with backoff
* [x] `5.17` Merge stream updates into the TanStack Query cache
* [x] `5.18` Live indicator and last-updated timestamp in the UI
* [x] `5.19` Close the stream on unmount and when a match ends

### Exit criteria

```text
[x] Live match opens
[x] SSE connection established
[x] Score update received
[x] UI updates automatically
[x] Browser refresh not required
[x] Connection reconnects after failure
[x] One live match produces one provider poll regardless of connected client count
[x] No connection or memory leak after repeated open/close cycles
```

The second-to-last criterion is the whole point of the architecture and should be verified explicitly, not assumed.

**Status: ✅ Complete**

### Sprint 5 notes

**Redis was not built.** The reasoning is in [D-014](./decisions.md), and the short version is that
with one API instance there is nothing to share between processes: a background service and an
in-memory subscriber list already satisfy the one-poll-many-clients criterion. `IMatchBroadcaster`
is the seam, so a Redis implementation drops in unchanged the day a second instance exists.

**Three rules keep a hundred calls a day survivable**, and the third was the one that needed
thought:

1. The poller does not run unless somebody is subscribed, so idle days cost nothing at all.
2. Its tick rate is not its spend rate. Every tick goes through the same five-minute cache the HTTP
   endpoints use, so ticking every thirty seconds costs at most twelve calls an hour and only
   shortens the gap between a cache refresh and the push that follows it.
3. **The browser closes its stream when the tab is hidden.** An open connection is what tells the
   server someone is watching, so without this a tab forgotten in a background window would drain
   the entire daily allowance on its own — 100 ÷ 12 is about eight hours. The poller also stops
   once only `ReservedHits` remain, so a long session degrades itself rather than starving someone
   opening a match page.

**Change detection could not use record equality.** `MatchDetailsDto` is a record, but its innings
are an `IReadOnlyList<T>`, and records compare list members by reference. A freshly mapped response
allocates new lists every poll, so every poll would have looked like a change and woken every
client. `MatchSignature` builds a comparable fingerprint of the things a watcher would notice
instead, and has a test asserting that two separately built copies of an identical match are not
equal as records but do share a signature.

**Verification.** Fan-out was tested with 25 concurrent subscribers, and the registry with 500
open/close cycles asserting it is left both empty and still usable. The heartbeat is tested with a
`FakeTimeProvider` rather than a sleep, because `8.26` calls proxy buffering the most likely
deployment failure and a flaky test there would be worse than none. The snapshot-on-connect and
end-on-completed frames were also confirmed against the running API with `curl -N`.

**What could not be verified against real data:** the provider's window held no live match on the
day this was built, so an actual score changing mid-stream has not been seen end to end. The path
is covered by tests at both the broadcaster and the controller, but the first genuinely live match
is worth watching.

---

# 📊 Sprint 6 — Scorecard + Commentary

**Goal:** depth on the match page. Scope is bounded by what `3.3` found.

* [ ] `6.1` `GET /api/matches/{matchId}/scorecard`
* [ ] `6.2` `GET /api/matches/{matchId}/commentary`
* [ ] `6.3` `GET /api/matches/{matchId}/stats`
* [ ] `6.4` Scorecard DTOs and mappers
* [ ] `6.5` `BattingTable`, responsive rather than horizontally scrolling
* [ ] `6.6` `BowlingTable`, responsive
* [ ] `6.7` Partnerships and fall of wickets
* [ ] `6.8` Over-by-over display
* [ ] `6.9` `Commentary` list with incremental loading
* [ ] `6.10` Match timeline
* [ ] `6.11` Live commentary updates via the Sprint 5 stream
* [ ] `6.12` Cache scorecard and commentary in Redis
* [ ] `6.13` Hide tabs and sections the provider cannot populate, rather than showing empty shells

### Exit criteria

```text
[ ] Scorecard renders for a completed match
[ ] Scorecard renders and updates for a live match
[ ] Commentary renders and appends live
[ ] Tables are readable on mobile without horizontal scroll
[ ] Unsupported data is hidden, not shown as broken or empty
```

---

# 🌍 Sprint 7 — Cricket Ecosystem

**Goal:** the platform around the match experience.

### Backend

* [x] `7.1` Series endpoints: list, detail, matches, standings — see notes
* [x] `7.2` Team endpoints: list, detail, matches — no players, see notes
* [ ] ~~`7.3` Player endpoints: list, detail, stats~~ — **no source exists**, see notes
* [x] `7.4` Provider methods and mappers for each — `series_id` and both sides, see notes
* [x] `7.5` ~~PostgreSQL~~ SQLite and EF Core setup — see notes
* [x] `7.6` Entities and migrations for teams, players, competitions, matches, venues — narrower, see notes
* [x] `7.7` Persist provider entities to reduce repeat external calls — finished matches only
* [x] `7.8` Search endpoint across matches, teams, and series — not players, see notes
* [x] `7.15` Paged `GET /api/matches/recent` reading the archive

### Frontend

* [x] `7.9` Series page: overview, matches, points table — teams and results are the matches
* [x] `7.10` Team page: overview, matches, series, opponents, formats — no players, no record, see notes
* [ ] ~~`7.11` Player page: profile, batting, bowling, recent matches~~ — **no source exists**, see notes
* [x] `7.12` `/matches` with filters for status, date, and series
* [x] `7.13` Search UI with debounced input
* [x] `7.14` Cross-entity navigation: match ↔ team ↔ series — stops short of players, see notes
* [x] `7.16` "Load more" on completed matches, with a count of what is held
* [x] `7.17` `GET /api/matches/series`, read from the matches that exist

### Sprint 7 notes (in progress)

**Started early, out of order.** Old completed matches disappearing was a visible gap rather than
a planned feature, so the persistence tasks were pulled forward ahead of the series, team and
player work they were written for.

**SQLite, not PostgreSQL yet.** A file rather than a service: nothing to install and nothing to run
alongside the API, which is the right size for the one table that exists. `IMatchArchive` is the
seam, so the swap at deployment changes a registration and the migration. Reasoning in
[D-017](./decisions.md).

**`7.7` is narrower than it reads.** Only finished matches are persisted, because a finished match
cannot change. Caching live data in the database would be a second cache with a harder invalidation
problem than the five-minute in-memory one already solves.

**The archive accumulates forward and cannot be backfilled.** No source available to us can supply
completed matches with results — CricketData's `recent-matches` returns the same short window under
another name, and Cricbuzz's listing carries no result sentence and only relative dates. History
therefore starts the day this shipped, which the results page states rather than hides.

**`GET /api/matches/recent` is now paged and returns an envelope instead of a bare array.** The one
breaking API change so far. `docs/api.md` has the new shape.

**Route and paging constants were pulled into one place each** while the endpoint changed shape:
`ApiRoutes` on the backend, `services/endpoints.ts` on the frontend, `PageRequest` for the page
bounds the controller and the service both used to declare.

**`7.1` and `7.9` are narrower than they read, and one part of them is wider.** A series is
assembled from the matches we hold rather than fetched: the provider's series endpoints were
measured first and return an index, not data — `endDate` was never an ISO date across 25 series,
squads were empty for all of them, and there are no standings anywhere in the payload. So the
series list, detail and matches cost no extra provider call, and `matchCount` honestly means
"matches we hold". Reasoning in [D-021](./decisions.md).

**Standings are the exception, and they are read from Cricbuzz against its `robots.txt`.** No
source available to us publishes a points table otherwise, and deriving one would mean inventing
each competition's points rules. That file disallows every agent it has not named and ours is not
named; proceeding was a deliberate decision by the project owner on the grounds that `robots.txt`
is a crawling convention rather than a licence term. It ships off, behind its own switch, cached
for three hours, never retried, and still identifying itself honestly — impersonating a permitted
crawler was never on the table. Full reasoning and the obligations it creates are in
[D-020](./decisions.md).

**Adding a `required` member to `MatchDto` broke the archive,** caught by a live run rather than
by the tests. The archive stores that DTO as JSON, so a required member is one that no previously
written row has, and every archived match silently failed to read back. New fields on that type
need a default. There is now a regression test.

**`7.2` and `7.10` are teams assembled the same way series are, minus two things the tasks asked
for.** No players, because none can be linked to a match, and **no won-lost record**, because the
provider states a result only as prose — a record parsed out of `"India won by 8 wkts"` would be a
guess published as a statistic. Formats and opponents come from mapped fields and are real;
`opponents` counts meetings, not head-to-head. Unlike `SeriesId`, the new archive columns were
**backfilled** with `json_extract`, because the payload already held both sides and leaving history
unreachable was avoidable here. Reasoning in [D-023](./decisions.md).

**`7.3` and `7.11` are struck out rather than deferred: there is no source.** Two calls settled it.
`players_info` returns `id`, `name`, `country` and an image with **no statistics of any kind**;
`series_squad` returns an **empty array**, so no player links to any match; and
`players?search=Kohli` returns Aseem, Abir, Aryaveer, Shashwat and Smriti Kohli — **not Virat**.
The response was `status: "success"`, not a plan rejection, so this is what the endpoint gives.
Building the pages would have meant inventing statistics. Evidence in [D-023](./decisions.md).

**`7.6` is narrower than it reads, for the same reason `7.7` is.** Teams, players, competitions and
venues are not normalised into tables. The archive keeps the provider's own shape as a JSON payload
and promotes to an indexed column only what something actually looks up by — now `SeriesId`,
`HomeTeamId` and `AwayTeamId`. Normalising the rest would mean deciding today how every field maps,
and any field mapped carelessly would be silently lost for good.

**`7.8` and `7.13` search what we hold, and say so.** Matches, teams and series; players are
absent and the UI states why rather than showing an empty group. It is a substring scan in memory
rather than a text index, because the searchable set is the window plus the archive — hundreds of
rows — and the window has to be fetched anyway. Results are grouped rather than interleaved, since
ranking a team against a match would need a relevance rule invented without evidence.

**`7.14` is match ↔ team ↔ series and stops there.** "Match to team to player" was the task; the
last hop has no data behind it. Every edge that exists is navigable in both directions.

**A tally now excludes what the caller already counted.** A match that finished minutes ago sits in
both the window and the archive, and the series and team tallies were adding both — a plausible
number that was quietly wrong. Reasoning in [D-022](./decisions.md).

**`7.12` filters apply to all three lists at once.** One `MatchFilter` described in the Application
layer, applied in memory to the provider's window and in SQL to the archive — two implementations
because filtering a page after reading it leaves holes in it, but one description so the two cannot
drift apart. Dates cross the wire as instants rather than as a date, because only the browser knows
which timezone's day the reader meant. Filter state lives in the URL, so a filtered view is
something you can send someone. Reasoning in [D-018](./decisions.md).

### Exit criteria

```text
[x] Series and team pages render real data — player pages have no source, see notes
[x] Points table renders correctly — when a source supplies one; absence renders as no section
[x] Search returns results across every entity type that exists here — matches, teams, series
[ ] Navigation between entities works in both directions
[ ] Database migrations run cleanly from empty
[ ] All new pages are responsive with loading, empty, and error states
```

---

# 🛡 Sprint 8 — Production Hardening

**Goal:** deployed, observable, and safe to leave running.

### Frontend

* [ ] `8.1` Route-level lazy loading and code splitting
* [ ] `8.2` Error boundaries
* [ ] `8.3` SEO metadata per page
* [ ] `8.4` Accessibility pass
* [ ] `8.5` Image optimization
* [ ] `8.6` Bundle analysis and budget

### Backend

* [ ] `8.7` Rate limiting
* [ ] `8.8` Review caching layers and TTLs end to end
* [ ] `8.9` Retry policies and timeouts audited across all provider calls
* [ ] `8.10` `CancellationToken` coverage audit
* [ ] `8.11` Health checks including Redis, PostgreSQL, and provider reachability
* [ ] `8.12` Structured logging review, no secrets or noise

### Security

* [ ] `8.13` Production CORS policy
* [ ] `8.14` Security headers
* [ ] `8.15` Input validation on every endpoint
* [ ] `8.16` Secret management via hosting environment, and `CRICKETDATA_API_KEY` as a GitHub Actions secret once a workflow needs it
* [ ] `8.17` API abuse protection

### Testing

* [ ] `8.18` xUnit coverage for services and mappers
* [ ] `8.19` Frontend component and hook tests
* [ ] `8.20` Integration tests for the API endpoints
* [ ] `8.21` Tests wired into CI as a merge gate

### Deployment

* [ ] `8.22` Deploy frontend to Vercel or Netlify
* [ ] `8.23` Deploy backend to the chosen .NET host
* [ ] `8.24` Provision managed PostgreSQL and Redis
* [ ] `8.25` Production environment variables
* [ ] `8.26` Verify SSE survives the production proxy — the most likely deployment failure
* [ ] `8.27` Provider attribution visible in the UI, and its terms re-read before going public
* [ ] `8.28` Smoke test the full live path in production

### Exit criteria

```text
[ ] Application is publicly reachable
[ ] Live scores update in production
[ ] Health checks report accurately
[ ] Lighthouse performance and accessibility reviewed
[ ] No secrets in the repository
[ ] CI blocks merges on failing tests
[ ] Attribution requirements satisfied
```

`8.26` deserves early attention. Some free hosting tiers buffer or terminate long-lived responses, which breaks SSE. Confirming this during Sprint 5 rather than Sprint 8 avoids a late architectural surprise.

---

## Risks

| Risk | Sprint | Mitigation |
| ---- | ------ | ---------- |
| ~~Provider lacks ball-by-ball commentary or full scorecards~~ **Confirmed in `3.3`** | 6 | Sprint 6 scope must be re-planned; `bbbEnabled` was `false` on every match observed |
| Free-tier request limit exhausted by polling | 5 | Change detection, adaptive interval, Redis fan-out, and the budget guard built in `3.16` |
| Provider's current-matches window is small and volatile | 4 | Ten matches one day and one the next; `/upcoming` and `/live` can legitimately be empty, so empty states are not edge cases |
| Host buffers or drops SSE connections | 8 | Test on the real host during Sprint 5 |
| Provider update frequency slower than expected | 5 | Set UI expectations from `3.4`; never imply sub-second data |
| Provider terms change | any | `ICricketDataProvider` keeps the swap cheap |

---

## Definition of Done

Every task inherits the checklist from `project-plan.md`. A sprint closes only when all its tasks meet it and its exit criteria pass.

---

## Status

| Sprint | Status |
| ------ | ------ |
| 1 — Foundation | ✅ Complete |
| 2 — UI Foundation | ✅ Complete |
| 3 — Cricket Data Integration | ✅ Complete |
| 4 — Home + Match | ✅ Complete |
| 5 — Live Engine | ✅ Complete (Redis deferred, [D-014](./decisions.md)) |
| 6 — Scorecard + Commentary | ⬜ Not Started |
| 7 — Cricket Ecosystem | 🟡 In Progress (persistence started early) |
| 8 — Production Hardening | ⬜ Not Started |

**Current sprint:** Sprint 7 — Cricket Ecosystem, persistence first
**Next action:** finish what the archive opened up — filters on `/matches` (`7.12`) now that there is a list long enough to need them. Sprint 6 is still unscheduled: `bbbEnabled` was `false` on every match observed in the Sprint 3 spike, so ball-by-ball commentary has no evidence behind it yet and re-scoping it comes before starting it.

Reasoning behind the choices below is recorded in [decisions.md](./decisions.md).

### Sprint 3 notes — what the provider spike actually found

**`3.1` The provider changed.** SportScore was measured and rejected; CricketData was chosen. Five providers were compared against real responses rather than landing pages, and three of the five advertised a free tier they did not have. Full comparison in [D-012](./decisions.md). The free plan is 100 calls a day, permanent, no card.

**`3.2` Captured responses** live in `.spike/` locally, with three real matches committed as test fixtures at `backend/tests/CricketLive.Infrastructure.Tests/Fixtures/currentMatches.json`.

**`3.3` What exists, and what does not.** This is the gating finding.

| We wanted | Provider gives |
| --- | --- |
| Match id, venue, format, start time | yes — `id`, `venue`, `matchType`, `dateTimeGMT` |
| Per-innings runs, wickets, overs | yes — `score[]` as `r` / `w` / `o`, one entry per innings |
| Team short names and logos | yes, for teams it has a profile for; domestic sides arrive without one |
| Result sentence | yes — `status`, shown verbatim |
| Scorecard, batters, bowler, commentary | **no** — gated behind `bbbEnabled`, `false` on every match observed |
| Toss, match summary | no |

Two data-quality problems had to be absorbed in the mapper. The `score[]` entries have an empty `team` field, so the batting side has to be read out of a free-text `inning` label — and the provider writes that label two different ways inside a single match, `"northamptonshire Inning 1"` alongside `"Middlesex,Northamptonshire Inning 1"`. And the match `name` packs teams, match description and series into one comma-separated string.

**`3.4` Update frequency could not be measured directly** — the provider's window held no live matches on either day of the spike. It matters less than expected, because CricketData state that free data is "always a few minutes behind real-time" regardless of plan. The binding constraint is our 100-call budget, not their refresh rate: **Sprint 5 should poll no faster than every five minutes**, which is roughly 96 calls across an eight-hour window of cricket.

**`3.5` Attribution** is not demanded by CricketData's published terms the way SportScore's badge is, but the terms have not been read in full and this is a licensing question rather than a technical one. Task `8.27` now requires re-reading them before anything is public.

### Sprint 3 notes — what was built

* One upstream call serves all three lists. `/api/matches/live`, `/upcoming` and `/recent` are partitions of a single `currentMatches` response, which is the property that makes 100 calls a day workable.
* A match already in that window costs nothing extra to open — the detail endpoint is only called for matches outside it.
* `CricketDataHitBudget` claims a call before each request, reconciles against the provider's own `hitsToday`, and refunds a claim when the resilience pipeline rejects the call without sending it.
* Concurrent cache misses share one in-flight request rather than each spending a call.
* Retry is capped at one attempt, not the standard three, because every attempt costs a call. A circuit breaker stops us spending the day's allowance on a dead endpoint.
* A match id must be a GUID before any provider call is made, which is validation and budget protection at once.
* 33 mapper tests run against the captured fixtures, including one that reconciles the attributed innings totals against the provider's own "won by an innings and 79 runs" sentence — independent evidence that the attribution is right rather than merely self-consistent.

### Sprint 2 notes

* The `docs/` set was rewritten for Cricket Live from another project's documentation ([D-011](./decisions.md)). `engineering-standards.md` is now binding for every remaining sprint.
* Design tokens are semantic (`surface`, `ink`, `line`, `brand`, `live`) and defined once in `index.css`. No component hardcodes a colour.
* Mock data is served through the real API function signatures ([D-009](./decisions.md)), so Sprint 4 changes function bodies rather than components. `features/matches/types.ts` is the contract Sprint 3 has to meet.
* `/matches` renders all three match lists rather than a placeholder, because the home page links to it. Filtering still arrives in Sprint 7.
* The Sprint 1 health banner was removed from the frontend. With mock data it would have reported the API as unreachable, which is misleading; the endpoint and its test remain on the backend.
* Verified at 360px: no horizontal overflow on any page, score tables fit inside their cards, tab keyboard navigation moves selection and focus, and the mobile menu toggles `aria-expanded` correctly.

### Sprint 1 notes

* Linting uses **oxlint**, not ESLint. The Vite React template ships oxlint by default as of `create-vite` 9, so this is the zero-config path rather than a deliberate departure from the stack table in `project-plan.md`.
* PostgreSQL and Redis were deferred as planned. Redis lands in Sprint 5, PostgreSQL in Sprint 7.
* The solution file is `CricketLive.slnx`, the XML solution format that the .NET 10 SDK now emits by default.
* HTTPS redirection is enabled outside Development only, so the Vite dev server can call the API over plain HTTP without certificate friction.
