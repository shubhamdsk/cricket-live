# Cricket Live — Decision Log

Significant technical decisions, newest first. Each entry records the choice, why it was made, and
what it costs. An entry is only revised by adding a new one that explains the change.

---

## D-011 — The reference documentation was adapted, not adopted

**Status:** accepted

The `docs/` set began as a copy of another project's documentation. It was rewritten for Cricket
Live rather than followed, because several of its rules encode that product's constraints rather
than general truth.

Kept: the layering discipline, the no-`any` rule, the deliberate loading/empty/error states, tokens
defined once with no hardcoded colour in a component, the decision-log habit, and the manual
verification checklist.

Not kept, and why:

- **Its frontend layout** (`lib/`, `components/ui/`, `<feature>.api.ts`, a service layer between
  hook and API). `project-plan.md` specifies a structure, Sprints 1 and 2 are built on it, and a
  service layer would currently hold nothing — our business rules are score formatting, which lives
  in a feature's `utils/`.
- **Its icon rule** (one hand-written `icons.tsx`, no library). `project-plan.md` picks Lucide, and
  a cricket app does not have a bespoke icon language to protect.
- **Its 40px control cap.** That product is a desktop shell; this one is watched on a phone, so the
  primary target is 44px on mobile and tightens from `sm`.
- **Its "no tests unless asked" rule.** See D-010.

**Cost:** two documents in `docs/` now describe a system we have not finished building, and they
have to be kept honest sprint by sprint rather than written once.

---

## D-010 — Existing tests stay, the suite does not grow until Sprint 8

**Status:** accepted

The backend test project and its CI gate remain and must keep passing. No new tests are added until
Sprint 8, with one exception: the SportScore mappers in Sprint 3.

Sprint time in the early sprints buys more from working software than from coverage of code that is
still moving. The mappers are the exception because they are pure functions over a shape we do not
control, they are the single most likely place for a provider change to break us silently, and the
captured fixtures needed to test them are a Sprint 3 deliverable anyway.

**Cost:** regressions in the middle sprints are caught by hand or not at all, and Sprint 8 will
have more to write than if tests had accumulated along the way.

---

## D-009 — UI is built against mock data behind the real function signatures

**Status:** accepted

Sprint 2 builds the interface before any provider exists. The fixtures live in the feature's
`mocks/`, but they are served through the functions the API layer will keep —
`getLiveMatches(signal)` returning `Promise<Match[]>` — resolving after a delay and honouring the
abort signal.

The alternative, importing fixtures directly into components, is quicker and leaves a rewrite in
Sprint 4. This way the loading and error states are exercised for real, cancellation works, and
when the endpoint lands only the function bodies change.

It also inverts a risk usefully: `features/matches/types.ts` becomes the contract the backend has to
meet, written from what the screens actually need rather than from whatever shape the provider
happens to return.

**Cost:** the types were written before anyone had seen a SportScore response, so Sprint 3 will
have to reconcile them — and where the provider cannot fill a field, the screen that assumed it has
to change.

---

## D-008 — HTTPS redirection is enabled outside Development only

**Status:** accepted

The Vite dev server calls the API over plain HTTP. With redirection on everywhere, that call was
sent to a self-signed HTTPS endpoint and the fetch failed with a certificate error that looks
nothing like its cause.

Production still redirects, because there the certificate is real and the redirect is a control.

**Cost:** a developer running the API in Development can reach it over HTTP, which is the point but
also means the production transport path is not exercised locally.

---

## D-007 — Linting is oxlint, not ESLint

**Status:** accepted

`project-plan.md` lists ESLint. The Vite React template ships oxlint as of `create-vite` 9, so
oxlint is what a new project gets with no configuration. It is substantially faster and covers the
rules we actually rely on.

This was the zero-configuration path rather than a considered preference. Moving to ESLint is a
package install and a config file if a rule we need turns out to be missing.

**Cost:** a smaller rule ecosystem than ESLint, and any rule that exists only as an ESLint plugin
is unavailable.

---

## D-006 — Redis and PostgreSQL are provisioned in the sprint that needs them

**Status:** accepted

`project-plan.md` lists both under Sprint 1 foundation. Neither was added: Redis arrives in Sprint 5
with the live engine, PostgreSQL in Sprint 7 with persisted teams and players.

Infrastructure that nothing reads from is not foundation; it is a service that can fail, a
connection string to manage, and a container to run, in exchange for nothing until several sprints
later. The foundation that mattered — the projects, the envelope, the pipeline, CI — is in place,
and adding a data store to a working system is a contained change.

**Cost:** Sprints 5 and 7 each carry provisioning work that would otherwise have been done once at
the start, including whatever free-tier setup the hosting provider requires.

---

## D-005 — Every endpoint returns the same envelope

**Status:** accepted

Responses are `{ success, data, message }`, with an optional `errors` array for validation. `success`
is the only thing that decides whether a call worked, so `data: null` on a success means "nothing to
return" rather than "something went wrong".

The frontend unwraps this in exactly one place, `services/apiClient.ts`, and throws `ApiError`
carrying the message and status. No screen parses a response shape.

The exception middleware deliberately does not write the envelope once a response has started. An
SSE stream is mid-flight by definition, and wrapping a half-sent stream in JSON produces something
no client can parse; the exception is rethrown instead and the connection drops, which is what a
streaming client is already built to handle.

**Cost:** an envelope is a little more ceremony than returning the object directly, and a caller
that ignores `success` and reads `data` will silently see `null` instead of an error.

---

## D-004 — We poll, detect change, and fan out; we do not pretend to be real-time

**Status:** accepted

A background service polls the provider, compares the result with the last known state, writes
Redis, and pushes over SSE only when something changed.

The system cannot be fresher than the provider, so the design makes that limit explicit rather than
hiding it. Change detection keeps an unchanged over from waking every connected browser, polling
backs off when nothing is live, and the interval is set from the provider's measured update
frequency rather than a guess — which is why measuring it is a Sprint 3 deliverable.

The user-facing consequence is a rule in the design system: screens say when they were last updated
and never claim ball-by-ball immediacy.

**Cost:** a fixed floor on latency that no amount of work on our side can lower, and a polling loop
that must be tuned per provider rather than a push we are simply handed.

---

## D-003 — Redis holds live state; PostgreSQL is the system of record

**Status:** accepted

Redis holds current scores, live match state, recent commentary, and cache entries under the key
scheme in `project-plan.md`. PostgreSQL holds what we persist: teams, players, competitions, venues,
and match metadata.

Redis is not the database. Anything in it can be rebuilt from the provider or from PostgreSQL, which
is what makes it safe to run on a free tier that may evict or restart.

**Cost:** two data stores to operate, and the discipline of deciding which one owns each piece of
data every time a new one appears.

---

## D-002 — The cricket provider sits behind `ICricketDataProvider`

**Status:** accepted

`ICricketDataProvider` lives in `Application`. `SportScoreProvider` and every SportScore response
model live in `Infrastructure/SportScore`, and those models are internal to that project.

SportScore's free tier, coverage, and commercial terms can all change, and a provider swap is
therefore a question of when rather than if. Behind the interface it is a new implementation and a
registration change; without it, it would reach the controllers and then the frontend.

The mappers are where the provider's quirks are absorbed, which is also why they are the one thing
we test before Sprint 8 (D-010).

**Cost:** an interface, a set of DTOs, and a mapping layer that a direct call would not need, plus
the ongoing discipline of not letting a convenient provider field leak through because it is easier.

---

## D-001 — Live updates use Server-Sent Events

**Status:** accepted

Scores reach the browser over SSE rather than WebSockets or SignalR.

The traffic is one-directional: the server has new scores, the client has nothing to say back. SSE
is plain HTTP, reconnects on its own, needs no protocol upgrade, and passes through infrastructure
that sometimes refuses websockets. A cricket scoreboard is the shape of problem SSE was designed
for.

**Cost:** one-way only, so anything interactive later needs a second mechanism; a per-connection
limit per domain on HTTP/1.1; and long-lived responses that some hosts buffer or terminate — which
is why Sprint 5 verifies the production host rather than leaving it to deployment.
