# Cricket Live — Backend Standards

How `backend/` is built. This document is the detail behind
[engineering standards](./engineering-standards.md) for the server side; the deployment-level view
is in [architecture](./architecture.md) and [system design](./system-design.md).

Stack: .NET 10, ASP.NET Core Web API, C#, EF Core, PostgreSQL, Redis, Server-Sent Events.

Related: [frontend](./frontend.md) · [API](./api.md) · [security](./security.md) ·
[decisions](./decisions.md)

---

## 1. Structure

Solution: `backend/CricketLive.slnx`.

```text
backend/
├── src/
│   ├── CricketLive.Api              controllers, middleware, DI wiring, SSE endpoints
│   ├── CricketLive.Application      use cases, DTOs, abstractions (ICricketDataProvider…)
│   ├── CricketLive.Domain           entities and invariants
│   └── CricketLive.Infrastructure   SportScore client, Redis, EF Core, background services
└── tests/
    └── CricketLive.Api.Tests
```

One API, not a set of services. The product is a read-heavy funnel in front of one external
provider; splitting it would add deployment surface and cross-service calls to a system whose hard
problem is the provider, not the process boundary. If that changes, it changes in a recorded
decision.

---

## 2. Clean architecture

| Layer | Contains | May depend on |
| --- | --- | --- |
| Domain | entities, value objects, invariants | nothing |
| Application | use cases, DTOs, abstractions (`ICricketDataProvider`, caches, clock) | Domain |
| Infrastructure | SportScore client and mappers, Redis, EF Core, background services | Application, Domain |
| Api | controllers, middleware, DI wiring | all of the above |

Dependencies point inward. The Application layer states what it needs as an interface and
Infrastructure supplies it.

Rules:

- **Application decides the use case.** A controller does not orchestrate.
- **Infrastructure is replaceable.** No business rule lives in an HTTP client, a repository, or a
  `DbContext`.
- **Api is thin.** Controllers bind, delegate, and map a result to a status code. No rules.
- **Domain stays small and honest.** Most cricket data is someone else's; we do not model what we do
  not own.

---

## 3. The provider boundary

This is the rule the codebase exists to protect.

```text
SportScore response  →  SportScoreProvider  →  Mapper  →  Application DTO  →  React
```

- `ICricketDataProvider` lives in `Application`. It is the only way anything above Infrastructure
  asks for cricket data.
- `SportScoreProvider` and every SportScore response model live in
  `Infrastructure/SportScore`, and those models are `internal`. If a SportScore type can be named
  from `Application` or `Api`, the boundary has already leaked.
- Mapping is explicit and tested. Mappers are pure functions over captured provider fixtures, which
  is why they are the one place we write tests before Sprint 8.
- A second provider is added by implementing the interface, not by touching a controller or the
  frontend.

The provider's own quirks — a missing field, a string where a number belongs, a status spelled
differently — are absorbed by the mapper. Nothing downstream should be able to tell which provider
answered.

---

## 4. Responses and errors

Every endpoint returns the same envelope:

```json
{ "success": true, "data": { }, "message": "Success" }
```

`success` is the only thing that says whether a call worked. `data` is `null` on failure, and may
also be `null` on a success that legitimately has nothing to return.

Expected failures are values, not exceptions: a use case returns a result carrying either a value or
an error, and the controller maps it to a status code. Exceptions are for the unexpected, and the
global exception middleware turns those into a 500 whose message says nothing about our internals or
the provider's.

| Situation | Status |
| --- | --- |
| Success | 200 |
| Invalid input, for example a malformed match identifier | 400 |
| The match, team, or player does not exist | 404 |
| Rate limit hit (Sprint 8) | 429 |
| Unhandled failure | 500 |
| The cricket provider is unreachable or refused us | 503 |

503 matters more here than in most systems. When SportScore is down, our API is not broken and the
caller did nothing wrong; saying so lets the frontend show "scores are temporarily unavailable"
rather than a generic error.

The exception middleware must not try to write an envelope once a response has started. An SSE
stream is mid-flight by definition, and wrapping a half-sent stream in JSON produces a response no
client can parse.

---

## 5. Caching and the request budget

The provider's free tier is a fixed budget per day, and every design choice on this side is measured
against it.

```text
Background service  →  polls the provider  →  detects change  →  writes Redis  →  fans out over SSE
```

- **One live match costs one poll**, no matter how many clients are connected. Clients read Redis
  through our API or receive an SSE push; they never cause a provider call.
- **Change detection comes before broadcast.** Unchanged state is not pushed, so a slow over does
  not wake every connected browser.
- **Polling backs off when nothing is live.** An idle day should cost almost nothing.
- **Finished data is cached hard.** A completed match's scorecard never changes; it should be
  fetched once.
- Redis holds live state and cache under the key scheme in `project-plan.md`. It is not the system
  of record.

PostgreSQL is the system of record for what we persist — teams, players, competitions, venues, and
match metadata — so that browsing the catalogue does not spend the provider budget.

---

## 6. Conventions

- Dependency injection for everything; no static mutable state.
- `async`/`await` throughout, with a `CancellationToken` accepted and passed down every path. A
  cancelled request must not keep calling the provider.
- Typed `HttpClient` for the provider, with a timeout and a retry policy with backoff. A retry storm
  against a rate-limited API makes an outage worse.
- Structured logging with named parameters. Log what happened, not a formatted sentence.
- Never log the API key, a full provider response, or anything that would let a log reader replay
  our credentials.
- Configuration through `IOptions`, bound and validated at startup. A missing provider key should
  stop the application, not surface as a 500 on the first request.

---

## 7. Background services and SSE

- A background service owns polling. It respects its `CancellationToken`, never throws out of its
  loop, and logs a failed cycle without dying.
- One SSE endpoint per match stream. On connect, the current state is sent immediately so a client
  is never staring at an empty screen waiting for the next change.
- A heartbeat keeps proxies from closing an idle connection, and connections are cleaned up on
  client disconnect.
- Whether the production host tolerates long-lived responses is verified during Sprint 5, not at
  deployment. Some free tiers buffer or terminate them, which breaks SSE silently.

---

## 8. Testing

The existing tests stay and must keep passing. The suite is not expanded until Sprint 8, with one
planned exception: the SportScore mappers, tested against captured provider fixtures, because they
are pure, high-risk, and cheap to cover.

---

## 9. Verification

```powershell
dotnet build
dotnet test
```

Then run it: Swagger loads, the endpoints answer, the logs are clean, and a forced provider failure
produces a 503 with a sensible message rather than a stack trace.
