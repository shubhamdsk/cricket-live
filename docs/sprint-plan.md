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

* [x] `1.1` Create folder structure: `frontend/`, `backend/`, `docs/`
* [x] `1.2` Add root `.gitignore` covering Node, .NET, IDE, and env files
* [x] `1.3` Expand `README.md` with setup and run instructions
* [x] `1.4` Create `develop` branch and set branch protection expectations
  — the branch existed from the first day; the protection did not, for eight sprints. Now both
  branches require `backend`, `frontend` and `scan` to pass before a merge. See below.

### Frontend

* [x] `1.5` Initialize React + TypeScript via Vite in `frontend/`
* [x] `1.6` Configure Tailwind CSS with the mobile-first breakpoints from the plan
* [x] `1.7` Configure ESLint and Prettier, wire `lint` and `format` scripts
* [x] `1.8` Set up React Router with placeholder routes for `/`, `/live`, `/matches`, `/match/:slug`
* [x] `1.9` Set up TanStack Query provider in `app/providers.tsx`
* [x] `1.10` Set up Zustand store skeleton in `store/`
* [x] `1.11` Create typed API client in `services/` reading `VITE_API_BASE_URL`
* [x] `1.12` Add `.env.example`

### Backend

* [x] `1.13` Create ASP.NET Core Web API in `backend/`
* [x] `1.14` Establish layered projects: `Api`, `Application`, `Domain`, `Infrastructure`
* [x] `1.15` Configure Swagger/OpenAPI
* [x] `1.16` Configure CORS for the Vite dev origin
* [x] `1.17` Add global exception handling middleware returning the standard API response shape
* [x] `1.18` Add the `ApiResponse<T>` envelope (`success`, `data`, `message`, `errors`)
* [x] `1.19` Configure structured logging
* [x] `1.20` Add `GET /api/health`
* [x] `1.21` Add `appsettings.Development.json` and `.env.example` equivalents, no secrets committed

### CI

* [x] `1.22` GitHub Actions workflow: frontend install, lint, build
* [x] `1.23` GitHub Actions workflow: backend restore, build, test

### Exit criteria

```text
[x] React application runs
[x] .NET API runs
[x] React successfully calls /api/health and renders the result
[x] Swagger works
[x] Tailwind classes apply
[x] Routing works
[x] CI is green on develop
```

**Not in this sprint:** PostgreSQL, Redis, the cricket provider, real UI design.

> The plan's Sprint 1 lists PostgreSQL and Redis setup. Both are deferred: Redis to Sprint 5 where it is first needed, PostgreSQL to Sprint 7 where persisted entities first appear. Standing up infrastructure we do not yet read from adds failure modes without adding value.

---

# 🎨 Sprint 2 — UI Foundation

**Goal:** a reusable design system and mocked pages, with no cricket API involved.

### Design system

* [x] `2.1` Define Tailwind theme: colors, typography scale, spacing, live-status accent
* [x] `2.2` Layout components: `Header`, `Footer`, `Navigation`, `Container`
* [x] `2.3` Common components: `Button`, `Card`, `Badge`, `Tabs`
* [x] `2.4` State components: `Skeleton`, `Spinner`, `EmptyState`, `ErrorState`
* [x] `2.5` Match components against mock data: `MatchCard`, `MatchStatus`, `TeamScore`

### Pages

* [x] `2.6` Home with Featured, Live, Upcoming, Recent, Popular Series sections
* [x] `2.7` Live matches page
* [x] `2.8` Match details shell with Summary / Scorecard / Commentary / Stats tabs
* [x] `2.9` Mock data fixtures typed with the DTO shapes Sprint 3 will produce
  — done, then deleted. The fixtures did their job as a shape agreement and were removed the
  moment Sprint 3 landed, because a repository that keeps invented match data around is one
  refactor away from rendering it. Nothing under `frontend/src` now holds fabricated scores.

### Quality

* [x] `2.10` Verify every page at mobile, tablet, laptop, desktop widths
* [x] `2.11` Keyboard navigation and visible focus states
* [x] `2.12` Loading, empty, and error states rendered for each section

### Exit criteria

```text
[x] Mobile, tablet, and desktop layouts verified
[x] No horizontal overflow at any breakpoint
[x] No nested scroll containers
[x] Components are reusable and independent
[x] Loading, empty, and error states exist
[x] Contrast and focus states pass a manual accessibility check
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

**Goal:** depth on the match page. Scope was bounded by what `3.3` found — and `3.3` bounded it to
nothing. **Partly reopened and partly built:** the scorecard half now exists against a second
source, the commentary half still does not. See the notes and the correction below them.

* [x] `6.1` `GET /api/matches/{matchId}/scorecard` — built against RapidAPI Cricbuzz,
  [D-027](./decisions.md)
* [ ] ~~`6.2` `GET /api/matches/{matchId}/commentary`~~ — source exists, allowance does not
* [ ] ~~`6.3` `GET /api/matches/{matchId}/stats`~~ — source exists, allowance does not
* [x] `6.4` Scorecard DTOs and mappers
* [x] `6.5` `BattingTable`, responsive rather than horizontally scrolling — a real `<table>` that
  scrolls within its own container rather than the page
* [x] `6.6` `BowlingTable`, responsive
* [x] `6.7` Partnerships and fall of wickets — fall of wickets rendered; partnerships are carried
  through the API but not yet shown, because the card is already long
* [ ] ~~`6.8` Over-by-over display~~ — the second source does serve over summaries; unaffordable
  to keep current
* [ ] ~~`6.9` `Commentary` list with incremental loading~~ — source exists, allowance does not
* [ ] ~~`6.10` Match timeline~~ — same
* [ ] ~~`6.11` Live commentary updates via the Sprint 5 stream~~ — streaming it would mean polling
  it, which is the one thing the allowance forbids
* [ ] ~~`6.12` Cache scorecard and commentary in Redis~~ — the scorecard *is* cached, in memory;
  Redis is still deferred per [D-014](./decisions.md)
* [x] `6.13` Hide tabs and sections the provider cannot populate, rather than showing empty shells
  — held: the scorecard section says there is none rather than rendering an empty table

### Sprint 6 notes — closed, with the endpoints asked directly

**`3.3` inferred this from a flag; this sprint confirmed it at the endpoint.** The earlier spike
saw `bbbEnabled: false` on every match it observed and concluded the data was gated. That was an
observation of the match list, not a test of the endpoint that would serve a scorecard, so before
closing the sprint the endpoints themselves were called:

| Call | Result |
| --- | --- |
| `match_scorecard` | `status: "failure"` — *"Scorecard … not found"* |
| `match_bbb` | `status: "failure"` — *"Not able to get BBB for match"* |
| `match_squad` | `status: "success"`, **0 squads** |
| `currentMatches` | `bbbEnabled: false`, `hasScorecard` absent |

Six matches have now been checked across two spikes and not one carries a scorecard. The refusals
are explicit failure responses naming the match, not empty successes, so this is the provider
declining rather than a mapping problem at our end.

**`6.13` is the one task that survived, and it was satisfied by omission.** The Sprint 2 shell
specified Summary, Scorecard, Commentary and Stats tabs. Those tabs were never built with real
data behind them, so there are no empty shells to hide — the match page shows a header, the batters
at the crease when a second source supplies them, the per-innings breakdown and match information,
each of which has data behind it.

**Cricbuzz publishes all of this, and reading it was considered and declined.** The points-table
reader already crosses that line once, deliberately and recorded in [D-020](./decisions.md), but it
is a single cached read every three hours behind a switch that ships off. A scorecard and live
commentary would mean frequent per-match traffic, repeated while a match is in progress — a
different order of imposition on a site that disallows us, not the same decision at a larger size.
Full reasoning in [D-024](./decisions.md).

**One correction, and it matters more than the rest of this section.** Everything above is true of
CricketData and was wrongly generalised into "this data cannot be had". It can. A later spike
measured a live scorecard advancing from `340/8` at ball `1121` to `342/8` at ball `1130` over
seventy seconds, with ball-by-ball commentary text and over summaries alongside it. Every task
struck out above is buildable from a source reachable today.

The sprint stays closed anyway, for two reasons that have nothing to do with availability: the
licensing objection above, which the measurement strengthens rather than weakens — a call every
thirty seconds for the length of every match is precisely the imposition it declines — and cost,
since the only tier that could actually run this site is $29.99 a month. The evidence and the
arithmetic are in [D-026](./decisions.md).

So read the struck-out tasks as *unfunded*, not *impossible*.

### Sprint 6 notes, second pass — the scorecard was built after all

The decision above was overruled by the project owner, who chose to build on the free tier rather
than pay for one or leave it. So the sprint reopened with a constraint the original plan never
imagined: **200 requests a month**, about 6.6 a day.

That number is what sorted the remaining tasks, and the sort was not a matter of taste:

- A **scorecard** is one request for a whole innings, and a *finished* scorecard is one request
  forever. Good value, so it was built.
- **Commentary** has to keep up to mean anything. At the measured nine balls per seventy seconds
  that is roughly 120 requests an hour, so the entire month buys three minutes of one match.
  Building it would produce a commentary feed that is permanently three overs stale and then
  stops. Not built.

The build reflects the budget everywhere: nothing polls the new source, the scorecard sits behind
a button rather than loading with the page, a live card is cached for five minutes, a finished one
for a day, and sixty requests a month are reserved so that one live match cannot eat the
allowance and leave every completed card blank. Reasoning and costs in [D-027](./decisions.md).

It ships **disabled**. The licensing objection in D-024 is unchanged — this is a reseller of a
scrape, not a Cricbuzz product — so a deployment has to switch it on deliberately.

**Verified against the live gateway, once.** With the source enabled and a match paired by hand,
`GET /api/matches/{id}/scorecard` returned both innings of a real T20 — 11 batters, 6 bowlers, 10
fall of wickets, 10 partnerships and extras per innings — with dismissals intact as prose
(`c Getkate b Mark Adair`, `run out (Monank Patel)`), strike rates and economies as strings, and
`isComplete: true` selecting the 24-hour cache. The budget then logged *"7 of 200 monthly calls
used"*, which is the gateway's own figure rather than ours, so reconciliation works too. The
unpaired case was checked first and returned 404 **without touching the gateway**, which is the
path that protects the allowance and the one that will run most often.

### Exit criteria

```text
[x] Scorecard renders for a completed match — MET
[x] Scorecard renders and updates for a live match — PARTLY: renders; refreshes every five
    minutes rather than continuously, which is the budget, not the code
[x] Commentary renders and appends live — VOID: source exists, allowance does not
[x] Tables are readable on mobile without horizontal scroll — PARTLY: the tables scroll within
    their own container rather than the page. A scorecard has six columns of figures and the
    alternative is stacked cards nobody can compare down a column
[x] Unsupported data is hidden, not shown as broken or empty — MET
```

Rewritten from the first pass, where four of five read VOID. Two are now met, two are partly met
and honest about why, and one is still void. Both partials are budget, not engineering: a paid
tier would close them without a line of code changing.

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
[x] Navigation between entities works in both directions  match ⇄ series and match ⇄ team
[x] Database migrations run cleanly from empty     done twice, most recently on the Singapore Neon
[x] All new pages are responsive with loading, empty, and error states  covered by 8.1–8.4
```

These three sat unticked long after they were true, and that cost something real: a stale plan
was read as the source of truth and produced a recommendation to build `7.12`, which already
existed. A checkbox nobody ticks is not a neutral omission.

---

# 🛡 Sprint 8 — Production Hardening

**Goal:** deployed, observable, and safe to leave running.

### Frontend

* [x] `8.1` Route-level lazy loading and code splitting
* [x] `8.2` Error boundaries
* [x] `8.3` ~~SEO metadata per page~~ — static site metadata, plus per-page document titles; see notes
* [x] `8.4` Accessibility pass
* [x] `8.5` Image optimization
* [x] `8.6` Bundle analysis and budget

### Backend

* [x] `8.7` Rate limiting
* [x] `8.8` Review caching layers and TTLs end to end — reviewed, no change needed
* [x] `8.9` Retry policies and timeouts audited across all provider calls — reviewed, no change needed
* [x] `8.10` `CancellationToken` coverage audit — reviewed, no change needed
* [x] `8.11` Health checks ~~including Redis, PostgreSQL,~~ and provider reachability — neither is used; see notes
* [x] `8.12` Structured logging review, no secrets or noise — reviewed, no change needed

### Security

* [x] `8.13` Production CORS policy
* [x] `8.14` Security headers
* [x] `8.15` Input validation on every endpoint
* [x] `8.16` Secret management via hosting environment, and `CRICKETDATA_API_KEY` as a GitHub
  Actions secret once a workflow needs it — the host half is done: Render supplies
  `CricketData__ApiKey`, `CricbuzzApi__ApiKey` and `ConnectionStrings__Archive`, and nothing is in
  the repository. The Actions half stays undone on purpose, because the condition attached to it
  never arrived: no workflow calls the provider. `keep-warm` hits our own `/api/health/live`, and
  the three build workflows compile and test. Adding the secret now would mean a credential
  readable by CI for no reason
* [x] `8.17` API abuse protection

### Testing

* [ ] `8.18` xUnit coverage for services and mappers
* [ ] `8.19` Frontend component and hook tests
* [ ] `8.20` Integration tests for the API endpoints
* [x] `8.21` Tests wired into CI as a merge gate — **the wiring was half-built and looked
  finished.** `dotnet test` has run on every pull request for weeks, so the suite appeared to be a
  gate. It was not: neither `master` nor `develop` had any branch protection, so a red check
  blocked nothing and a merge went through regardless. Both branches now require `backend`,
  `frontend` and `scan`. `8.18`–`8.20` stay open under the standing no-new-tests instruction —
  this task was only ever about the wiring, and the wiring is real now

### Deployment — done, on Render, Neon and Vercel

**The site is live.** The API runs on Render, the archive in Neon PostgreSQL and the frontend on
Vercel; all three are free and none asked for a card. The guide is
[deployment.md](./deployment.md) and the reasoning behind the database is
[D-028](./decisions.md).

* [x] `8.22` Deploy frontend to Vercel or Netlify — Vercel, from `master`
* [x] `8.23` Deploy backend to the chosen .NET host — Render, Docker, Singapore
* [x] `8.24` Provision managed PostgreSQL and Redis — PostgreSQL on Neon. **Redis was not
  provisioned and is not needed**: [D-014](./decisions.md) keeps live state in process, and the
  deployment is one instance, so the condition that would call for Redis has not arrived
* [x] `8.25` Production environment variables
* [x] `8.26` Verify SSE survives the production proxy — **measured, it survives**; see below
* [x] `8.27` Provider attribution visible in the UI, and its terms re-read before going public —
  **read in full, and the re-read earned its place**: attribution turns out not to be required at
  all, and the site was breaking a different clause instead. See [D-030](./decisions.md) and below
* [x] `8.28` Smoke test the full live path in production

### Exit criteria

```text
[x] Application is publicly reachable            Vercel frontend, Render API, both responding
[x] Live scores update in production             verified on IND vs WI, 2nd ODI
[x] Health checks report accurately              /live and /ready both 200 against Neon
[x] Lighthouse performance and accessibility reviewed  98/100/100/100, see below
[x] No secrets in the repository                 secret scan on both branches, now actually required
[x] CI blocks merges on failing tests            203 backend tests, plus lint and build, required
[x] Attribution requirements satisfied           none are imposed; the credit is given anyway
```

### What re-reading the provider's terms found — `8.27`

The task was written expecting to find an attribution requirement the footer might not meet. It
found the opposite, and something worse elsewhere.

**No attribution is owed.** No clause obliges an API consumer to display a credit. The footer stays
because a project reading someone else's data for free should say so, not because it is made to.

**The site was hot-linking the provider's images.** Their terms forbid it in as many words —
*"Hot-linking of images we serve is not allowed ... Your domain may get blacklisted if you do
this"* — and team crests were `<img src="https://g.cricapi.com/...">` in two components, so every
visitor was spending their bandwidth. The penalty is aimed at the domain, so this was a way to lose
the API, not just the pictures. Crests are now fetched once by `/api/crests/...` and served from our
own origin, with archived rows rewritten on read so history is covered too. [D-030](./decisions.md)
has the full reasoning, including why the route is not an open proxy.

**Two standing conditions came out of it.** The free licence is personal and non-commercial, and
their definition of commercial covers "any remuneration, whether in money or otherwise" — so
advertising, donations or any paid use means buying credits first. And they explicitly disclaim
copyright in match data as "purely factual information", which is firmer ground for the archive
than the blanket copyright clause on the same page would suggest.

**The lesson is about the five sprints, not the clause.** Sprint 3 wrote down a guess — that
attribution was probably not required — flagged it as a guess, and it then survived unchallenged
through a public deployment. The guess was right. The page it was guessing about contained a
prohibition the project went on to break for five sprints.

### What the first deployment measured

Four things were being carried as risks or assumptions. Three resolved in the deployment's
favour, which is worth recording precisely because the prediction was the other way.

**`8.26` — SSE survives the proxy.** Flagged since Sprint 5 as the biggest deployment risk.
Measured with a plain HTTP client so the browser could not confound it: the connection stayed
open past **100 seconds** with keepalive frames arriving every 20, and the `match` event was
delivered immediately rather than buffered. Render does not buffer `text/event-stream` and does
not cut idle connections.

The browser looked worse than this at first — seven `stream` requests lasting between 1 and 13
seconds. That was contention, not a fault: several tabs were open on the same match and the
endpoint permits four concurrent streams per caller. The server's own log shows the pattern
resolving as tabs closed, ending with a single connection held for 111 seconds.

**Cricbuzz pairing works unaided.** [D-027](./decisions.md) recorded the scorecard as verified
only through a hand-written match id, leaving automatic resolution as the feature's weakest
link. On the deployed API it resolved `2nd ODI / West Indies tour of India, 2026` from Cricbuzz's
listing on its own and returned a live card — West Indies 255/2 in 32.4, John Campbell 101 off
68. The prediction that Cricbuzz would refuse a datacenter address was also wrong.

**Enabling the scorecard turns on more than it says.** `CricbuzzMatchDirectory` performs the
pairing by reading Cricbuzz's public website, and it is registered unconditionally rather than
behind `Cricbuzz:Enabled`. So turning on `CricbuzzApi:Enabled` starts the very scraping that the
other switch appears to govern. That is a larger step than the setting's name suggests, and it
bears on the unresolved attribution question in D-027.

**Co-locating the database is worth doing.** Render is in Singapore and the Neon project was
created in Ohio. Every archive query logs at exactly **202ms** — for a single-row primary-key
lookup, which should be about 1ms. A constant to three digits is distance, not work. The fix is
to recreate the Neon project in Singapore, and the time to do it is while the archive is nearly
empty.

Still unverified: that the archive survives a container spin-down. It needs twenty idle minutes
and has not been left alone that long yet.

### The Lighthouse pass, and the bug it found

Run against the **production build** served by `vite preview`, not the dev server, because an
unminified bundle with HMR attached makes the performance number meaningless.

| Page | Performance | Accessibility | Best Practices | SEO |
| --- | --- | --- | --- | --- |
| Home | 98 | 100 | 100 | 100 |
| Match details | 98 | 100 | 100 | 100 |

Home: FCP 1.8 s, LCP 2.1 s, TBT 60 ms, CLS 0. Match details: FCP 1.8 s, LCP 2.0 s, TBT 10 ms,
CLS 0.037.

The first run scored 96/100/96/91, and two of the three gaps were real problems rather than
scoring artefacts.

**The production build could not reach the API at all.** Three requests went to
`undefined/api/matches/live`. Vite inlines `import.meta.env.VITE_*` at build time, and an unset
variable does not fail the build — it becomes the literal string `undefined`. `.env.development`
covers `npm run dev`, nothing covered a production build, and `npm run build` had been reporting
success while producing a bundle that could not fetch anything. This is exactly the failure `8.25`
would have hit on the day of deployment, silently. `vite.config.ts` now validates the variable and
refuses to build without it, also rejecting a non-absolute URL and a trailing slash; CI passes an
explicit stand-in value with a comment saying it is one.

**`robots.txt` reported 43 errors** because there was no such file, so the single-page app's
catch-all served `index.html` and Lighthouse parsed HTML as robots directives. Added, with a note
in it explaining why there is no `Sitemap` line: routes live in the URL fragment and are never
sent to a server.

**One contrast failure, which only appeared once the API worked.** The `completed` badge was
`ink-subtle` on the muted badge background — 4.34:1 against a 4.5:1 floor at 12px. That colour is
fine on white, which is why it had survived; the muted background is what pushed it under. Now
`ink-muted`, which makes `completed` and `neutral` render identically.

Remaining imperfect audits are all performance and all acceptable: `unused-javascript` reports
~56 KiB, which is router and vendor code the other pages need, and the LCP of ~2 s is a local
`vite preview` with no compression and no CDN.

`8.26` deserves early attention. Some free hosting tiers buffer or terminate long-lived responses, which breaks SSE. Confirming this during Sprint 5 rather than Sprint 8 avoids a late architectural surprise.

## Sprint 8 notes — what was reviewed, and what was left alone

Four tasks — `8.8` to `8.10` and `8.12` — asked for audits rather than features, and an audit that
finds nothing is a real result as long as it says what it looked at. All four are recorded as
reviewed with no change, on this evidence:

| Task | What was checked | Finding |
| --- | --- | --- |
| `8.8` | every `cache.Set` call site | all six pass an explicit TTL; no entry can outlive its window. Per-match entries are bounded in number by the call budget, since each one costs a provider call to create |
| `8.9` | the resilience pipeline and both Cricbuzz clients | one retry and a circuit breaker on CricketData, because an attempt costs a call; deliberately no retry on Cricbuzz, where a failed read loses two player names |
| `8.10` | the whole of `src` for `CancellationToken.None`, `.Result`, `.Wait()` and `GetAwaiter()` | no occurrences; every path takes a token and passes it on |
| `8.12` | every `Log*` call site | all structured with named placeholders, none interpolated. No key, no request URI — only paths, and `Request.Path` without the query string, so search terms stay out of logs too |

Two things did change under `8.15`, both bounds that were missing rather than checks that were
wrong. A search term now has a maximum length as well as a minimum, since Kestrel would otherwise
allow an 8 KB term to be matched against every field of every match and then echoed back. And a
team slug longer than the column that stores it is answered directly instead of becoming an
oversized query parameter that cannot match. Everything else was already validated: the match and
series routes require a GUID, the filter query is parsed before use, and page requests are clamped.

`8.3` could not be done as written and was narrowed rather than skipped. Routes live in the
fragment ([D-019](decisions.md#d-019)), so a crawler or a link unfurler is served the same document
for every URL on the site and never learns which page was asked for. Per-page meta tags would
therefore be written for an audience that cannot read them. What ships instead is honest about that
split: static site-level description and Open Graph tags in `index.html`, which reach the one URL
anybody can actually fetch, and per-page document titles, which reach browser tabs, history,
bookmarks and screen readers. There is no `og:image`, because there is no asset to point at and a
tag resolving to a 404 makes a worse preview than no tag.

`8.4` turned out to be mostly about navigation rather than markup. The components were already in
good shape — labelled landmarks, `aria-expanded` on the menu toggle, focus rings, 44px targets — but
a client-side navigation is not a page load, so the browser did none of what it normally does: the
title was never re-read, focus stayed on the link that was clicked, and to a screen-reader user the
app appeared not to have responded. A live region now announces the page, and focus moves to the
`<main>` landmark on every route change.

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
| 6 — Scorecard + Commentary | 🟡 Scorecard built and live; commentary will not be built, [D-027](./decisions.md) |
| 7 — Cricket Ecosystem | ✅ Complete (player pages have no source, [D-018](./decisions.md)) |
| 8 — Production Hardening | ✅ Deployed, hardened, provider terms settled ([D-030](./decisions.md)) |

**Every task in this plan is now ticked except `8.18`–`8.20`**, which are deferred under a standing
instruction not to write new tests. `8.21`, the wiring those three would hang off, is done: both
branches require `backend`, `frontend` and `scan`, so the 203 tests that already exist are a real
merge gate rather than a report nobody was obliged to read ([D-032](./decisions.md)).

**Next action:** none is outstanding. The build work is finished, and what is left is a judgement
call rather than a task — whether to unset `CricbuzzApi__Enabled` in production, where the
recommendation on the record is yes. Anything beyond that is new scope, not remaining scope.

**One thing was reported as a bug and turned out to be the design.** The series page showed a
single series, because the list was built only from matches we hold and the provider's window held
two matches of one tour. Nothing was failing; the specification was. The provider's `series` index
is now read for existence — 1190 series are listed there, against the 2 matches the window had —
which took the page from 1 series to 63 and search for "india" from 2 results to 13. The earlier
decision to ignore that endpoint measured it correctly and drew the wrong conclusion from the
measurement; both halves are recorded in [D-033](./decisions.md).

The spin-down question is closed. It had sat on this list as "needs twenty idle minutes nobody has
spent yet", and the reason nobody had spent them was that keep-warm made them impossible to spend.
Disabling it and waiting 43 minutes produced the first genuine double cold start: `/api/health/ready`
answered `Healthy` in 26.3 s with both Render and Neon suspended, so startup migrations do survive
a sleeping database. Measurements in [deployment.md](./deployment.md).

**The Cricbuzz review is now done too, in [D-031](./decisions.md), and it did not come out in the feature's favour.** Neither route to that data is licensed — Cricbuzz grants its site for "private viewing only" and RapidAPI's terms put the licence between the consumer and a publisher who is not Cricbuzz. The wrong default that [D-029](./decisions.md) found is fixed: `Cricbuzz:AutoResolve` was `true`, so switching on the scorecard was enough to start reading Cricbuzz's website, and it is now off like every other path to that site. What remains is not technical — whether to leave `CricbuzzApi:Enabled` set in production is the project owner's call, and the recommendation on the record is not to.

The Neon project has since moved to Singapore, which took archive queries from 218ms to 2–3ms — see [D-029](./decisions.md).

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

**`3.5` Attribution** is not demanded by CricketData's published terms the way SportScore's badge is, but the terms have not been read in full and this is a licensing question rather than a technical one. Task `8.27` now requires re-reading them before anything is public. **Confirmed in Sprint 8** — the guess was correct, and the same page turned out to forbid something the project was doing; see [D-030](./decisions.md).

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
