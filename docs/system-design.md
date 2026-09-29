# Cricket Live — System Design

The upper-level design: what the system must be good at, how it is decomposed, how data flows, and
what it does when something breaks. The code-level view of what exists today is in
[architecture.md](./architecture.md).

---

## 1. System context

```text
       CricketData                      Browser
      cricket provider                (phone first)
             │                              │
             │ HTTPS, metered               │ HTTPS
             ▼                              ▼
   ┌──────────────────────────────────────────────┐
   │              Cricket Live API                │
   │  provider client · use cases · REST · SSE    │
   └───────────┬───────────────────┬──────────────┘
               │                   │
          ┌────▼────┐        ┌─────▼──────┐
          │  Redis  │        │ PostgreSQL │
          │ live +  │        │  catalogue │
          │  cache  │        │            │
          └─────────┘        └────────────┘
```

We own everything inside the box. The cricket itself belongs to someone else, and that asymmetry
drives every design decision below.

---

## 2. What this system must be good at

In priority order:

1. **Showing a live score quickly and correctly**, on a phone, without a refresh.
2. **Spending as few provider requests as possible.** The free tier is a hard daily budget shared by
   every visitor.
3. **Staying up when the provider is not.** A provider outage should degrade the product, not break
   it.
4. **Being honest about freshness.** We can never be fresher than our source.
5. **Surviving a provider change** without a rewrite.

Everything else — series pages, player statistics, search — is breadth, and breadth is easier than
any of the above.

---

## 3. Scale assumptions

A portfolio-scale product on free tiers, designed so the shape does not have to change if it grows.

| Dimension | Assumption |
| --- | --- |
| Concurrent viewers | Tens today; the design should hold at thousands |
| Live matches at once | Usually 0–5, occasionally more during a tournament |
| Provider budget | On the order of 10,000 requests per day, shared by everyone |
| Read:write ratio | Effectively all reads; we write nothing a user creates |

The number that governs the design is not requests per second to our API — it is **provider
requests per live match per minute**, which must stay flat as viewers increase.

---

## 4. Decomposition

One API, four projects, dependencies pointing inward: `Api` → `Application` → `Domain`, with
`Infrastructure` supplying what `Application` declares.

It is not split into services. The product is a read funnel in front of one provider; separate
processes would add deployment surface and network hops without separating anything that is
genuinely independent. The seam that matters is not between services, it is between us and the
provider, and that seam is `ICricketDataProvider` ([D-002](./decisions.md)).

| Component | Owns |
| --- | --- |
| Provider client | Talking to CricketData, mapping its shapes to ours |
| Background service | Polling live matches, detecting change, writing Redis |
| REST API | Reads, served from Redis or PostgreSQL where possible |
| SSE endpoint | Pushing changes to connected clients |
| Redis | Live state and cache |
| PostgreSQL | Catalogue: teams, players, competitions, venues, match metadata |

---

## 5. Key flows

**Someone opens a live match.**

```text
Browser → API → Redis (current state) → response
Browser → API → SSE connection → current state immediately, then changes as they happen
```

No provider call happens on this path. That is the whole design: viewers are free, only matches
cost.

**A score changes.**

```text
Background service → provider → compare with last state
   unchanged → stop
   changed   → write Redis → push to every SSE connection for that match
```

**Someone opens a finished match.**

```text
Browser → API → cache or PostgreSQL → response
```

A completed match never changes, so it should be fetched once and then never again.

---

## 6. Data design at the upper level

Three stores, three jobs:

- **The provider** is the source of cricket truth. We never argue with it.
- **Redis** holds what is changing now: live state, current score, recent commentary, hot cache. Any
  of it can be rebuilt, which is what makes it safe on a tier that may evict or restart.
- **PostgreSQL** holds what is stable: the catalogue, so browsing does not spend provider budget.

Redis is not the database and PostgreSQL is not a cache. When a new piece of data appears, deciding
which one owns it is part of the work.

---

## 7. Real-time design

Server-Sent Events, one stream per match ([D-001](./decisions.md)).

The traffic is one-directional and the client has nothing to say back, so SSE's simplicity is a
feature: plain HTTP, automatic reconnection, no protocol upgrade to negotiate with a proxy.

Fan-out is the point. One poll feeds every connection for that match, so the provider cost of a
match is independent of how many people are watching it. Change detection sits before the broadcast
so an unchanged poll wakes nobody.

The known hazard is infrastructure: some hosts buffer or terminate long-lived responses. Sprint 5
verifies this against the real host rather than discovering it at deployment.

---

## 8. Consistency model

Eventually consistent, bounded by the provider's own update frequency.

A client's view can lag the provider by up to one poll interval, and the provider itself lags the
actual cricket. We do not hide this: screens state when they were last updated, and the design
system forbids copy that implies ball-by-ball immediacy ([D-004](./decisions.md)).

On connect, an SSE client is sent the current state immediately, so a late joiner is never staring
at an empty screen waiting for the next wicket.

---

## 9. Failure modes and degradation

| Failure | Behaviour |
| --- | --- |
| Provider unreachable | Serve last known state from Redis; new requests answer 503 with a human message. The site stays up |
| Provider rate limit hit | Back off, keep serving cached state, log loudly. This is an operational incident |
| Redis unavailable | Fall back to calling the provider on read, with tighter limits. Degraded and more expensive, not broken |
| PostgreSQL unavailable | Live scores keep working; catalogue pages degrade |
| SSE connection drops | The client reconnects with backoff and is re-sent the current state |
| Background service crashes | It logs and continues on the next cycle; a failed poll never kills the loop |

The ordering principle: a live score is the product. Everything else may degrade to keep it working.

---

## 10. Observability

Structured logging from the start. The metrics that will matter, when they arrive in Sprint 8:

- Provider requests per hour, against the daily budget — the single most important number
- Cache hit ratio, because it is the inverse of the one above
- Active SSE connections, and how long they last
- Poll cycle duration and failures
- Endpoint latency and error rate

Health checks report the API, Redis, PostgreSQL, and provider reachability without leaking their
addresses.

---

## 11. Deployment topology

```text
Vercel or Netlify  →  static React build
Free .NET host     →  the API and its background service
Managed PostgreSQL →  catalogue
Managed Redis      →  live state and cache
```

Single region, single instance. The background service runs in-process with the API, which is
correct at one instance and becomes wrong at two — a second instance would double the provider
polling. Scaling out therefore means separating the poller first, and Redis is already the shared
state that makes that possible.

---

## 12. Explicit non-goals

- Not a betting, fantasy, or prediction product.
- Not generating cricket data of our own, or correcting the provider's.
- No user accounts, notifications, or personalisation in the MVP.
- Not multi-region, not multi-instance, not horizontally scaled at MVP.
- Not real-time in the sub-second sense, and it does not claim to be.

---

## 13. Trade-offs taken

| Choice | Bought | Paid |
| --- | --- | --- |
| Poll and fan out | Provider cost independent of audience | A latency floor we cannot lower |
| SSE over websockets | Simplicity, reconnection, proxy tolerance | One-way only; host buffering risk |
| One API, not services | Less deployment surface, no cross-service calls | A single process to scale |
| Provider behind an interface | A provider swap is a registration change | An interface, DTOs, and mappers to maintain |
| Redis plus PostgreSQL | Right store for each job | Two dependencies to operate |
| Defer infrastructure to the sprint that needs it | A working system at every step | Provisioning work spread across sprints |
