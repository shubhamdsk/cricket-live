# Cricket Live — Decision Log

Significant technical decisions, newest first. Each entry records the choice, why it was made, and
what it costs. An entry is only revised by adding a new one that explains the change.

---

## D-015 — Batters come from a second source that is off by default

**Status:** accepted

The question was whether to replace CricketData with a self-hosted scraper to escape the hundred
calls a day. The answer is no, and the reason is structural rather than a matter of taste: the
scraper proposed (`mskian/live-cricket-score-api`) exposes exactly one route, `GET /?score={id}`,
which needs a Cricbuzz match id. It cannot answer "what is on right now", so it cannot implement
`GetCurrentMatchesAsync`, so it cannot be a replacement for anything.

It was run anyway, against real pages, because arguing from a README is not evidence. Of five
matches sampled, none returned fully clean data and every failure was reported as HTTP 200 with
`"status": "success"`:

- team abbreviations over four letters were silently truncated — `INDWA 136/4` came back as `NDWA`
- Test matches, with two innings in the title, could not be parsed at all
- every bowler name carried page furniture, as in `Tushar Deshpande View match performance View profile`
- a lone batter was discarded entirely, because it kept results only in pairs
- names were HTML-escaped rather than decoded, so an O'Brien would reach the browser as `O&#x27;Brien`

One field survived: the batters at the crease, which is also the one thing CricketData does not
give us. So that field, and only that field, is taken — from a C# port rather than by running the
Python service, because the useful logic is about forty lines and porting it costs no new runtime,
no third process, and lands the parsing in the test suite where a markup change fails CI instead of
quietly returning placeholder text to users. The five defects above are fixed in the port and each
has a test.

**Only the `og:title` meta tag is read, never the page body.** That is what makes the bowler
pollution impossible rather than merely fixed: a meta tag has no link labels next to the name.
Anything that cannot be taken from that tag belongs to the primary provider.

**We identify ourselves honestly.** The original sends a browser user agent with `Referer` and
`Origin` set to cricbuzz.com so its traffic reads as the site's own. That is evasion and it is not
reproduced; a test asserts we send neither header. Cricbuzz was verified to answer an honest agent
with HTTP 200, so the disguise bought nothing anyway. If an honest agent is ever blocked, that is
an answer about whether the data is ours to take, and the response is to stop rather than to hide.

**It ships disabled, and matches are mapped by hand.** Whether to read a public website is a
judgement about someone else's terms, not a technical default, so `Cricbuzz:Enabled` is `false`.
The two providers share no key, and guessing the pairing from team names and dates fails silently
by showing one match's batters on another match's page — worse than showing none. The hand-written
map doubles as the rate limiter: load is bounded by an act of typing rather than by how popular the
app becomes.

**Cost:** the port reads a presentation detail of someone else's HTML and will break without
warning. The tests are the alarm, and the feature degrades to absence rather than to error — an
empty list renders as no section at all, never as "nobody is batting", because those are different
claims and we only know the first.

**Reopen this when:** Cricbuzz blocks an honest agent, the parser tests start failing for reasons
other than our own changes, or a provider appears that supplies batters under terms that permit it.

---

## D-014 — Live state stays in process; Redis waits for a second instance

**Status:** accepted
**Amends:** D-003, which assigned live state to Redis
**Applies:** D-006, which provisions infrastructure in the sprint that needs it

Sprint 5 was planned as Redis plus a poller plus SSE. It shipped the poller and SSE, and no Redis.

Redis earns its place when there is something to share between processes. There is one API
instance, nothing is deployed, and the sprint's own hardest exit criterion — one live match
produces one provider poll regardless of how many clients are connected — is met by a background
service and an in-memory subscriber list. Adding Redis today would introduce a network hop, a
serialization format, a connection to supervise and a service to run, in exchange for nothing
observable.

The seam is where it needs to be. `IMatchBroadcaster` is the only thing the poller and the stream
endpoint know about, so the day a second instance exists, a Redis-backed implementation replaces
`MatchBroadcaster` and nothing else changes.

**Reopen this when any of these becomes true:**

- more than one API instance runs, so a client connected to A must see a poll made by B
- live state has to survive a restart rather than being re-fetched
- the subscriber list outgrows what one process should hold in memory

**Cost:** a restart drops every open stream. Clients reconnect with backoff and the endpoint sends
current state on connect, so the visible effect is a brief "Reconnecting…" rather than a blank
page. Nothing durable is lost, because nothing here is the system of record.

---

## D-013 — A section the data cannot support is removed, not stubbed

**Status:** accepted
**Follows from:** D-012

The Sprint 3 spike found that the provider carries no toss, no editorial summary, and no batters or
bowler at the crease. Sprint 2 had built all four, because Sprint 1 assumed a provider would supply
them. The choice was to keep them behind empty states or delete them.

They are deleted, along with the Scorecard, Commentary and Stats tabs that had never held anything.
An empty state is a promise that content belongs there and is temporarily absent. When it is
permanently absent, the promise is false, and a page that keeps making it reads as broken rather
than as finished. A match page with a header, an innings breakdown and match information is
complete; the same page with three tabs saying "arrives in Sprint 6" is the same content with a
notice that it is not.

This applies to real gaps too, not just permanent ones. If the provider has no venue for a match we
show nothing in that line rather than "Venue: unknown", and a team with no crest gets no
placeholder shape — the row reads the same either way.

**Cost:** UI gets deleted and later rebuilt. The Sprint 6 scorecard work now starts from nothing
instead of from a tab that already existed. That is the right trade: the tab cost about an hour and
was misleading every day it shipped. It also means a screen cannot be built more than a sprint
ahead of the data behind it, which is a constraint on planning, not just on code.

---

## D-012 — CricketData replaces SportScore as the cricket provider

**Status:** accepted
**Supersedes the provider named in:** D-002, `project-plan.md`

`project-plan.md` names SportScore. The Sprint 3 spike measured it and four alternatives against
real responses, and SportScore turned out to be unusable for cricket.

What was measured, not read off a landing page:

| Provider | Free tier | One call returns all live scores | Innings with overs | Verdict |
| --- | --- | --- | --- | --- |
| **CricketData / CricAPI** | 100/day, permanent | yes | yes | **chosen** |
| SportScore | keyless, ~10k/day | yes | no — one string, `"54/7"` | rejected |
| Cricwix | 7-day trial, 100/day | — | — | rejected |
| Big Balls Data | 250/day (500 with GitHub) | no — one call per match | yes | rejected |
| Roanuz | limited trial | — | — | rejected |

SportScore returns `incidents: []`, `stats: []` and `lineups: null` on every live cricket match, has
no venue, no format and no over count, and reports a Test match's score as `"-"`. Its `status_text`
— the field our design treats as the provider's authoritative sentence — returns values such as
`"Abnormal"` and `"Cut in half"`. Cricwix advertises 1,000 calls a day with "no trial clock" and
delivers a seven-day trial at 100. Big Balls Data has excellent scorecards but serves live state one
match at a time, so a five-match evening exhausts its allowance before lunch.

CricketData wins on one structural property rather than generosity: `/currentMatches` returns every
match in the window with per-innings runs, wickets and overs **in a single call**. Our cost per
refresh is therefore flat no matter how much cricket is being played, which is the only reason a
hundred calls a day is survivable. `/api/matches/live`, `/upcoming` and `/recent` are partitions of
that one response, not three upstream requests.

**Cost:** a hundred calls a day caps refresh at roughly five minutes, which Sprint 5 must design
around rather than against. The provider states its free data is "always a few minutes behind
real-time" regardless of plan, so paying would raise volume without improving freshness. Ball-by-ball
is gated behind a `bbbEnabled` flag that was `false` on every match observed, so Sprint 6's scorecard
and commentary scope is not yet supported by any evidence.

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
Sprint 8, with one exception: the provider mappers in Sprint 3.

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

**Cost:** the types were written before anyone had seen a provider response, so Sprint 3 had to
reconcile them — and where the provider cannot fill a field, the screen that assumed it has to
change. In the event they held up well: runs, wickets, overs, venue, format and team short names
all exist. Toss, summary, current batters and current bowler do not, and those sections come out.

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

`ICricketDataProvider` lives in `Application`. The implementation and every provider response model
live in `Infrastructure/<Provider>`, and those models are `internal` to that project — the API
cannot reference them even by accident.

A provider's free tier, coverage, and commercial terms can all change, so a swap is a question of
when rather than if. Behind the interface it is a new implementation and a registration change;
without it, it would reach the controllers and then the frontend. D-012 is that swap happening
before the first line of provider code was written, which is the cheapest moment it could have.

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
