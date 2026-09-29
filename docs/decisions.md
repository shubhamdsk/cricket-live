# Cricket Live — Decision Log

Significant technical decisions, newest first. Each entry records the choice, why it was made, and
what it costs. An entry is only revised by adding a new one that explains the change.

---

## D-023 — Teams are assembled from matches; players are not built at all

**Status:** accepted

`/api/teams` follows [D-021](#d-021--a-series-is-assembled-from-matches-not-fetched) and for a
stronger reason: **the provider issues no team identifier and has no team endpoint.** A match
names its two sides and that is the whole of it, so a team is not a record to fetch and decorate —
it is what the matches say, and nothing else could be true of it.

Identity therefore comes from the name, as a slug. That is weaker than the series case, where an
opaque `series_id` arrives free, and the weakness is real: a side that changes how its name is
spelled becomes a second team. The slug is at least stable under spacing and punctuation, and it
is the value `TeamDto.Id` already carried before any of this existed, so nothing new was invented.

**Unlike `SeriesId`, the team columns were backfilled.** Adding indexed columns to the archive
normally leaves existing rows empty — that is what happened for `SeriesId`, and it means history
is unreachable through the new column. Here the payload already held both sides, so the migration
extracted them with SQLite's `json_extract` rather than writing off every match archived before
today. Without it a team's first match would appear to be whenever the columns happened to be
added. This is the second provider-specific line in the project after the `NOCASE` collation, and
it is confined to a migration, which is the one place a SQL dialect is expected to show.

### There is no won-lost record, and its absence is the decision

The provider states a result only as prose — `"India won by 8 wkts"`, `"Match tied"`,
`"No result"`. Turning that into a record means parsing free text and then publishing the parse as
a team's history. The formats a team played and the sides it faced come from mapped fields and are
facts; who won does not, so the page reports what was played and says nothing about how it fared.
A wrong record would be indistinguishable from a right one to a reader, which is exactly why it is
not offered.

### Players: no source, so no feature

Tasks `7.3` and `7.11` asked for player endpoints and a player page with profile, batting, bowling
and recent matches. **Two calls established that none of it is available**, and the evidence is
worth recording because the tasks look reasonable until you look:

| Checked | Result |
| --- | --- |
| `players_info` fields | `id`, `name`, `country`, `playerImg` — **no stats of any kind** |
| `series_squad` | `data: []`, so no player is linked to any match we hold |
| `players?search=Kohli` | 10 hits: Aseem, Abir, Aryaveer, Shashwat, Smriti — **not Virat** |
| Mentions of `matchId` or `recent` in a player payload | 0 |

So a player cannot be reached from a match, a match cannot be reached from a player, and a player
profile would hold a name and a flag. The response came back `status: "success"` rather than as a
plan rejection, so this is what the endpoint returns rather than something a paid tier is
withholding — though that distinction is worth re-testing if the plan ever changes.

Building the pages anyway would mean inventing statistics, which is the one thing this project does
not do. The tasks are recorded as blocked with this evidence instead, and search says plainly that
players are not searchable rather than returning an empty group and letting the reader wonder.

**What this costs:** `7.14`'s "match to team to player" stops at the team. Cross-entity navigation
is match ↔ team ↔ series, which is every edge the data actually supports.

---

## D-022 — A tally excludes what the caller already counted

**Status:** accepted

`GetSeriesTalliesAsync` and `GetTeamTalliesAsync` take the ids the caller is counting from the
provider window and leave those rows out of the aggregate.

A match that finished minutes ago is in **both** the window and the archive. The services add a
window-derived tally to a SQL-derived one, so without this a recently-finished match is counted
twice and a series or team claims more matches than it has. The bug was silent: the number is
plausible, only wrong, and it grows worse the more often the poller runs.

The exclusion list is the window — a few dozen ids — so it is a short `NOT IN` rather than
anything needing a temporary table, and an empty list adds no clause at all.

`GetSeriesNamesAsync` passes an empty list deliberately: it reduces to a distinct set of names, so
a duplicate costs nothing and `Distinct` already removes it. Nothing there is counted.

---

## D-021 — A series is assembled from matches, not fetched

**Status:** accepted

`/api/series` is derived from the matches already in hand — the provider's current window plus the
archive — and costs no call beyond the one every other list already shares.

**The provider's series endpoints were measured first, and they are an index rather than data.**
Four calls, spent deliberately:

| Endpoint | Returned |
| --- | --- |
| `series` | 25 per page: id, name, start/end, format counts |
| `series_info` | an info block and an empty `matchList` |
| `series_squad` | `data: []` |
| `players` | id, name, country, and nothing else |

Three findings decided it. **`endDate` was never an ISO date** — 0 of 25, always `"Apr 11"` with
no year, and in the 7 cases where `startDate` was ISO the two fields disagreed on format *inside
the same object*, so the year would have to be inferred from the series name. **`squads` was 0 for
all 25**, which is why `series_squad` came back empty. And 18 of 25 declared ODI, T20 or Test
counts while reporting `matches: 0`. Reading any of this would spend from a hundred-a-day budget
to learn less than the matches already in memory can say.

**`series_id` is the one field worth taking, and it arrives free** on every match. It is what
makes a series identifiable: the name reaches us as the tail of a free-text field, and two matches
of one series do not reliably spell it the same way, so grouping by name would split a series in
half. Ids group; names display.

**`matchCount` means matches we hold, and the UI says "held" rather than implying completeness.**
A tournament that started before the archive did will show a fraction of what it played. That is
the same limitation the results page already states about its own history.

**Adding `SeriesId` as a `required` member broke every row already archived,** which a live run
caught before this shipped: `MatchDetailsDto` is what the archive stores as JSON, and a required
member is one that no previously written payload has. Every archived match failed to deserialise
and silently vanished from results. The field is now optional with a default, and a regression
test writes a payload without it. **Anything added to `MatchDto` from now on needs a default for
the same reason** — the archive's durability is the whole point of it.

---

## D-020 — Standings are read from Cricbuzz, against its robots.txt

**Status:** accepted, deliberately and with the cost understood
**Supersedes part of:** D-015

A points table is read from Cricbuzz series pages. Off by default, behind its own switch.

**Standings cannot be derived and cannot be bought.** CricketData publishes no table at all — the
full `series_info` payload was searched for `points`, `standing`, `table`, `nrr`, `wins` and
`losses`, and matched none of them. Computing one from results would mean encoding each
competition's points rules, and the County Championship alone awards bonus points for batting and
bowling. A derived table is a guess presented as a standing, which this project does not do.
Cricbuzz publishes one: the Ranji Trophy Elite table is 740 KB of server-rendered HTML, four
groups, no JavaScript assembly.

**Cricbuzz's `robots.txt` disallows us, and we are proceeding anyway.** The file reads:

```
User-agent: *
Allow: /ads.txt
Disallow: /
```

It then grants broad access to roughly eighteen *named* agents — Googlebot, Bingbot, Applebot,
Twitterbot, AmazonAdBot and others. Ours is not among them. This is not a site that failed to
consider automated readers; it is one that decided which ones it wants. The judgement taken here
is that `robots.txt` is a crawling convention rather than a licence term, and the project owner
made that call with the file in front of them.

**What is not on the table is sending a user agent the file permits.** Declining a stated
preference and impersonating someone granted an exception are different acts, and only the first
is being done. We identify ourselves as `cricket-live/1.0` with no `Referer` and no `Origin`,
exactly as D-015 established, and a test asserts it.

**The obligations this creates are in the code, not in good intentions:**

- **Off by default,** under `Cricbuzz:StandingsEnabled`, separate from the switch that governs
  batters because it is a separate and larger decision. When off, the reader is not registered at
  all rather than registered and dormant.
- **Cached for three hours,** not seconds. A table changes when a match finishes.
- **No retry, ever.** A page that did not answer is not an invitation to ask again.
- **One listing read serves every series**, so resolution is a request per cache period rather
  than per page view.
- **The live test is opt-in** behind `CRICKET_LIVE_LIVE_TESTS=1`, so CI does not send this traffic
  from every branch on every push.

**A wrong table is worse than no table,** so the parser is built to decline. It reads the header
rather than assuming column positions, because competitions differ — Ranji publishes P, W, L, NR,
Pts and NRR with no tie column, and a fixed layout would shift every value after the missing one.
A row whose numbers do not parse is dropped rather than defaulted to zero, since a zero is a claim
about a result. Anything unreadable becomes an empty list, which renders as no section at all.

**Cost:** this reads a presentation detail of someone else's HTML and will break without warning.
The tests against a captured page are the alarm.

**Reopen this when:** Cricbuzz blocks an honest agent — in which case stop, per D-015 — or a
source appears that publishes standings under terms that permit it.

---

## D-019 — Routes live in the fragment

**Status:** accepted
**Extends:** D-018

`createHashRouter` rather than `createBrowserRouter`. URLs read `/#/matches?status=completed`.

**A path router needs the host to cooperate.** Every unknown path has to be rewritten to
`index.html`, or reloading `/matches` is a 404 from the server before the app ever loads. That
rewrite is a different setting on every host — a `_redirects` file, a `try_files` directive, a
rewrite rule — and it is easy to deploy without. When it is missing, what breaks is reload, shared
links and the back button, which are precisely the three things D-018 put filter state in the URL
to preserve. The fragment is never sent to the server, so none of it can go wrong.

**Search params still work.** They live inside the fragment and `useSearchParams` reads them
unchanged; a deep link with two filters was verified restoring both.

**Cost, and it is a real one: crawlers.** Search engines index the path, not the fragment, so every
route collapses to one indexable URL. For a public scores site that is a genuine loss, and it is
the reason this would be worth revisiting. Reopen if organic search becomes a goal, or when the
deployment target is known to rewrite reliably — the change is one function name and nothing else,
because no code reads `window.location` or builds a path by hand.

Nothing else was affected: links are `<Link to="/matches">` and react-router adds the `#` itself,
and the API base URL is unrelated to how the app routes.

---

## D-018 — One filter, applied twice, and dates cross the wire as instants

**Status:** accepted
**Extends:** D-017

Matches live in two places now — the provider's window in memory and the archive on disk — and
filtering has to mean the same thing in both.

**The filter is described once and applied twice.** `MatchFilter` carries the four conditions and
owns the in-memory predicate; `SqlMatchArchive` translates the same conditions into a `WHERE`. Two
implementations were unavoidable, because filtering the archive after reading a page gives a page
with holes in it — ask for twenty, discard nine, serve eleven, and the count no longer agrees with
what came back. What was avoidable is two *descriptions*, which is how a LINQ predicate and a SQL
clause end up quietly disagreeing about case sensitivity. The tests for the two halves cover the
same cases deliberately.

**Dates cross the wire as instants, not as a date.** A calendar day is a different interval in
every timezone: the same match starts on the 27th in London and the 28th in Sydney. A server given
`date=2026-09-27` has to guess whose 27th that is, and any guess is wrong for most readers. The API
therefore takes `from` and `to` as instants and the browser — the only participant that knows the
reader's timezone — converts its local day into them. The range is half-open so consecutive days
abut without overlapping or leaving a gap, and a match starting exactly at midnight belongs to the
later day only.

**Series is matched whole, not as a substring.** `tour of India` would pull in every touring series
at once, which is a filter quietly becoming a search and returning more than was asked for.

**Case-insensitivity comes from the column's collation, not from the comparison.** `NOCASE` on
`SeriesName` keeps the match index-backed while agreeing with `OrdinalIgnoreCase` in memory.
`LIKE` was the obvious alternative and is the wrong tool twice over: a series named `100% Cricket
League` would become a wildcard matching everything, and `LIKE` is case-insensitive in SQLite but
case-sensitive in PostgreSQL. This is the one provider-specific line in the model, and it is in the
model precisely so the PostgreSQL swap has one place to look.

**The series list is read from the rows, never held as a list**, and drawn from both sources since
neither is a superset of the other. The filter can then only offer selections with something behind
them.

**Contradictions are answered, not rejected.** Asking the archive for live matches, or `/upcoming`
for live ones, returns empty — a coherent question with an empty answer. The second case returns
empty *without calling the provider*, which matters because provider calls are the budgeted
resource and a client sending one filter to all three lists should not spend calls on the two it
excluded. An inverted date range is different and returns **400**: it selects nothing, and serving
an empty list for it would look like an answer.

**Filter state lives in the URL.** A filtered view is then linkable, reloadable and reachable with
the back button. Component state would break all three silently. Changes `replace` rather than
`push`, so adjusting a dropdown does not bury the previous page under a dozen history entries.

**Cost:** four new query parameters on three endpoints, one new endpoint, and a migration for the
collation. A filter naming a series that has since aged out of both sources shows an empty list;
the filter bar keeps the name visible as an option so the reader can see what they are filtered to
rather than facing a blank dropdown above nothing.

---

## D-017 — Results are kept in a SQLite file, and only from today forward

**Status:** accepted
**Extends:** D-012

The provider's current-matches window is a few days wide, so "recent results" meant "results since
the day before yesterday" and nothing more. A match that finished last week was gone, and so was
its match page, because the only place we had ever held it was the window.

**We keep what passes through rather than fetching history.** No source available to us can supply
completed matches with results. CricketData's `recent-matches` route was measured and returns the
same short window under a different name; Cricbuzz's own listing mixes live and upcoming fixtures,
carries no result sentence in the one part of the page we are willing to read, and dates entries
relatively ("Today", "Yesterday"). Anything we presented as older history would therefore have been
assembled from fragments, and a wrong result is worse than a missing one.

So the archive **accumulates forward**. It began empty on the day it shipped. Matches played before
that cannot appear, which is stated on the results page rather than hidden, and `total` in the API
is explicitly "what we hold" rather than "what was played".

**SQLite because it is a file.** Nothing to install, nothing to run alongside the API, nothing to
provision. A few thousand finished matches a year is not a workload that needs more. PostgreSQL is
still the deployment target from D-004; `IMatchArchive` is the seam, so that swap changes one
registration and the migration, and nothing above Infrastructure.

**Each match is stored whole, as JSON, beside a handful of indexed columns.** The columns — id,
slug, start time, series — are the ones we sort, page and look up by. The payload is the entire
serialised match and is what gets returned. Normalising instead would mean deciding today how every
field maps, and any field mapped carelessly would be silently lost for good; this way an archived
match reads back byte-identical to a live one, and a column can be promoted out of the payload
later without a backfill.

**Archiving is a decorator over the data provider, not a step inside it.** Fetching cricket and
keeping cricket are different jobs. It is deliberately not in the live poller either, because the
poller only runs while somebody is watching a live match — history would then depend on whether
anyone happened to be watching. Every window fetch passes through the decorator, so anyone opening
the site contributes.

**A failed write never fails a read.** The decorator logs and returns the provider's data. Losing a
match from history is a much smaller harm than a home page that will not load, and the results
endpoint separately merges any finished match the window still holds but the archive does not, so
a failed write costs history rather than today's results.

**Cost:** `GET /api/matches/recent` is now paged and returns an envelope instead of a bare array,
which is a breaking change to that one endpoint. A finished match is never rewritten, so a later
provider correction to a completed match will not be picked up — accepted, because a finished match
does not change, and keeping what we recorded at the time is the more defensible of the two.

---

## D-016 — Matches are paired on title and series, or not at all

**Status:** accepted
**Extends:** D-015

Enrichment needs to know which Cricbuzz match is which of ours, and the two providers share no
identifier. The first version required somebody to write each pair into configuration, which meant
no match was ever enriched unless a human had been watching.

Measuring a real listing of 26 Cricbuzz matches against our window settled how to do better, and
also ruled out the obvious approach:

**The team pair is not a key.** `ind` appeared on three of the 26 fixtures, `skr` on four, and two
were `tbc-vs-tbc` because Cricbuzz lists finals before the finalists are known. Our own
`IND v WI, 1st ODI` matched two entries on teams alone — the 2nd and 3rd ODIs, **both wrong**,
because the 1st had already dropped off the listing. That join does not fail; it puts another
match's batters on the page and looks right doing it.

**The slug tail is a key, and we can reproduce it exactly.** Cricbuzz links read
`/live-cricket-scores/151543/ind-vs-wi-2nd-odi-west-indies-tour-of-india-2026`. Everything after
the teams is the match title and series name, and slugifying CricketData's own `matchTitle` and
`seriesName` produces the identical string once punctuation is dropped. Across the 26 fixtures this
gave 24 distinct values, the only two collisions being group-stage fixtures sharing "Pool A" and
"Pool B" within one tournament — and those have different teams, so teams plus tail is unique
across the whole listing.

So: exact match on title and series; teams consulted only to break a collision, because the two
providers do not always abbreviate alike and demanding agreement up front would reject good matches.

**A unique answer or no answer.** Zero candidates means no enrichment. Two candidates the teams
cannot separate means no enrichment. There is no best guess, because declining costs two player
names and guessing tells the reader something false about a match they are watching.

**Cost:** one request for a listing that serves every match, cached for thirty minutes, rather than
one request per match. Which fixtures exist changes over hours; only the scores move quickly, and
those are fetched separately. `Cricbuzz:MatchIds` survives as an override for when resolution
declines, and `Cricbuzz:AutoResolve` turns the whole mechanism off independently of `Enabled`,
because it is a separate risk from reading the site at all.

**Verified** against live Cricbuzz: resolution found `155422` for "2nd unofficial Test / Australia A
tour of India 2026" from the title and series alone. The unit tests use the real 26-match listing,
including both `tbc-vs-tbc` fixtures and the Pool A collision.

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

**It ships disabled.** Whether to read a public website is a judgement about someone else's terms,
not a technical default, so `Cricbuzz:Enabled` is `false`.

**Matches are paired on title and series, and never by guesswork.** See D-016.

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
