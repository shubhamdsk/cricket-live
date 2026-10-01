# Cricket Live — API

The implemented HTTP surface. Endpoints are added here as their sprint lands; the planned surface is
in [`project-plan.md`](../project-plan.md).

Base URL in development: `http://localhost:5140`.

Last updated: Sprint 3.

---

## Conventions

**Envelope.** Every endpoint returns the same shape, camelCase:

```json
{ "success": true, "data": { }, "message": "Success" }
```

One route is exempt, because its caller is an `<img>` rather than our client: `/api/crests/...`
returns image bytes or a bare 404. See [Crests](#crests).

On failure, `data` is `null` and `message` carries text safe to show a person. `success` is the only
thing that says whether a call worked: `data` is also `null` on a success that has nothing to
return. Validation failures add an `errors` array; it is omitted otherwise.

**Authentication.** None. The MVP is a public read-only API. Anything that changes this needs a
decision entry and a pass over [security.md](./security.md).

**Status codes.**

| Code | Meaning |
| --- | --- |
| 200 | Success |
| 400 | Validation failure, for example a malformed match identifier |
| 404 | The match, team, player, or series does not exist |
| 429 | Rate limit hit (Sprint 8) |
| 500 | Unhandled failure. The envelope says nothing about our internals |
| 503 | The cricket provider is unreachable, refused us, or our daily allowance is spent |

503 is the one worth stating early. When the provider is down, this API is not broken and the caller
did nothing wrong, so the frontend can say "scores are temporarily unavailable" instead of showing
a generic failure.

**CORS.** An allow-list from `Cors:AllowedOrigins`, never a wildcard. Development allows the Vite
dev server only.

**Client rules.** The frontend unwraps the envelope in exactly one place,
`services/apiClient.ts`, which throws `ApiError` carrying the message and status. No component
calls `fetch`. See [frontend.md §2](./frontend.md).

---

## Health

### `GET /api/health`

Liveness for the API itself. Takes no parameters.

```json
{
  "success": true,
  "data": {
    "status": "Healthy",
    "environment": "Development",
    "timestampUtc": "2026-09-28T14:09:22.347Z"
  },
  "message": "Success"
}
```

Dependency health — Redis, PostgreSQL, and provider reachability — is added in Sprint 8. Today this
endpoint answers only "the process is up and serving".

---

## Matches

### Filtering

All three list endpoints accept the same four parameters. None are required, and omitting them all
means "everything".

| Parameter | Notes |
| --- | --- |
| `status` | `live`, `upcoming` or `completed`. Redundant on `/live` and `/upcoming`, accepted there so one filter can be sent to all three. |
| `from` | Inclusive lower bound on start time, as an ISO instant. |
| `to` | Exclusive upper bound, so consecutive days abut without overlapping. |
| `series` | An exact name from `GET /api/matches/series`. Matched whole and case-insensitively. |

**`from` and `to` are instants, not dates**, and this is deliberate. A calendar day is a different
interval in every timezone — the same match starts on the 27th in London and the 28th in Sydney —
and the server has no way to know which one the caller meant. Converting a local day into a range
is therefore the client's job:

```text
GET /api/matches/recent?from=2026-09-27T00:00:00%2B05:30&to=2026-09-28T00:00:00%2B05:30
```

**`series` is a filter, not a search.** `tour of India` matches nothing; the whole name must be
given. A substring would silently widen the result to every touring series at once.

A range where `from` is not earlier than `to` returns **400**. It selects nothing, and serving an
empty list for it would look like an answer rather than a mistake.

### `GET /api/matches/live` · `GET /api/matches/upcoming`

Two lists, each returning `data` as an array of matches. They are partitions of a
single upstream response, so asking for both costs one provider call rather than two — see
[D-012](./decisions.md).

A `status` that contradicts the endpoint — `/upcoming?status=live` — returns an empty array
**without calling the provider**, which matters because provider calls are the budgeted resource.

**Either can legitimately return an empty array.** The provider's current-matches window held
ten matches one day and one the next, and no live matches at all across two days of the Sprint 3
spike. An empty list means no cricket in the window, not a failure.

```json
{
  "success": true,
  "data": [
    {
      "id": "90ae280c-cb10-4d58-9bcc-ec95294819e6",
      "slug": "india-vs-west-indies-90ae280c-cb10-4d58-9bcc-ec95294819e6",
      "status": "completed",
      "format": "ODI",
      "seriesName": "West Indies tour of India, 2026",
      "matchTitle": "1st ODI",
      "venue": "Greenfield International Stadium, Thiruvananthapuram",
      "startTimeUtc": "2026-09-27T08:30:00+00:00",
      "home": {
        "team": { "id": "india", "name": "India", "shortName": "IND", "logoUrl": "/api/crests/…" },
        "innings": [{ "number": 1, "runs": 300, "wickets": 2, "overs": "41.4" }]
      },
      "away": {
        "team": { "id": "west-indies", "name": "West Indies", "shortName": "WI", "logoUrl": "/api/crests/…" },
        "innings": [{ "number": 1, "runs": 295, "wickets": 7, "overs": "50" }]
      },
      "statusText": "India won by 8 wkts"
    }
  ],
  "message": "Success"
}
```

Field notes worth knowing before building against this:

- `status` is one of `live`, `upcoming`, `completed`. `format` is `T20`, `ODI`, `TEST` or `OTHER` —
  the provider covers formats we do not model, such as T10.
- **`format` comes from `matchTitle` whenever the two disagree.** The provider's own `matchType`
  labelled all five T20Is of the West Indies tour as `odi`, so a title naming a format outranks it;
  see [D-034](./decisions.md). A title naming no format, like `1st Match`, leaves `matchType` to
  decide.
- `overs` is a **string** in cricket notation, where `41.4` means 41 overs and 4 balls. It is never
  arithmetic.
- `innings` is an array because a Test side bats twice. It is empty for a side that has not batted.
- `statusText` is the provider's own sentence. Display it verbatim; never paraphrase it.
- `logoUrl` is nullable — the provider only has images for teams it holds a profile for — and is a
  **path on this API, not a whole address**. Resolve it against the API's base URL the same way a
  fetch would be. See [Crests](#crests).
- `slug` always ends in `id`, so a pretty URL resolves without a lookup.

### `GET /api/matches/recent`

Completed matches, newest first. **This is the one list that is paged**, because it is the one list
that grows: live and upcoming come from the provider's few-day window, while results come from what
we kept as that window moved on — see [D-017](./decisions.md).

| Parameter | Default | Notes |
| --- | --- | --- |
| `page` | `1` | 1-based. Anything below 1 is treated as 1. |
| `pageSize` | `20` | Clamped to 1–50. |

`data` is an envelope rather than a bare array. The matches inside `items` are exactly the objects
the two lists above return.

```json
{
  "success": true,
  "data": {
    "items": [{ "id": "90ae280c-…", "status": "completed", "statusText": "India won by 8 wkts" }],
    "page": 1,
    "pageSize": 20,
    "total": 1,
    "hasMore": false
  },
  "message": "Success"
}
```

`total` is what the archive currently holds **that matches the filter**, so it shrinks as a filter
narrows and grows as matches finish. **It is not the number of matches ever played.** The archive
accumulates forward from the day it was switched on, so an early `total` being small is expected
rather than a sign of missing data. Page through with `hasMore` rather than by comparing counts.

Filtering happens in the database rather than over the returned page, so a filtered page is a full
page and `total` agrees with what came back.

### `GET /api/matches/series`

Every series that currently has a match behind it, sorted, as `data`:

```json
{ "success": true, "data": ["West Indies tour of India, 2026"], "message": "Success" }
```

Read from the matches themselves rather than kept as a list, and drawn from **both** the provider's
window and the archive, because neither is a superset of the other: the archive has not heard of a
tournament that started this morning, and the window has forgotten one that ended last week. A
fixed list would go stale the first time a tournament ended, and would offer selections returning
nothing.

### `GET /api/matches/{matchId}`

Accepts either the bare id or the full slug. Returns one match with two extra fields:

```json
{ "hasBallByBall": false, "hasSquads": true }
```

These report what the provider claims to hold for this match rather than what we display. Across
every match observed during the spike `hasBallByBall` was `false`, which is why commentary is not
built. It has no bearing on the scorecard below, which comes from somewhere else entirely.

Returns **404** when the identifier is not a GUID, or is a well-formed GUID that neither the
provider nor the archive knows. A malformed identifier is rejected without any provider call, which
protects the daily allowance as much as it validates the input.

A match the provider's window has dropped is still answered from the archive if we kept it, so a
link to a finished match does not rot the moment the window moves past it.

Every match also carries `seriesId`, the provider's own identifier for the series it belongs to.
It is **empty when the provider sent none**, and such a match has no series page to link to. Build
series links from this rather than from `seriesName`, which is parsed from a free-text field and
is not a reliable key.

### `GET /api/matches/{matchId}/scorecard`

The full card: every innings with both batting and bowling, extras, fall of wickets and
partnerships.

```json
{
  "matchId": "1a2b…",
  "status": "Australia A won by 4 wkts",
  "isComplete": true,
  "innings": [
    {
      "inningsNumber": 1,
      "battingTeamName": "India A",
      "battingTeamShortName": "INDA",
      "runs": 342,
      "wickets": 8,
      "overs": 96.4,
      "runRate": 3.53,
      "isDeclared": true,
      "batting": [
        {
          "name": "N Jagadeesan",
          "runs": 119,
          "balls": 205,
          "fours": 12,
          "sixes": 1,
          "strikeRate": "58.05",
          "dismissal": "c Konstas b Doggett",
          "isCaptain": false,
          "isKeeper": true
        }
      ],
      "bowling": [
        { "name": "B Doggett", "overs": "22.4", "maidens": 5, "runs": 71, "wickets": 3, "economy": "3.13" }
      ],
      "extras": { "byes": 4, "legByes": 9, "wides": 3, "noBalls": 1, "penalty": 0, "total": 17 },
      "fallOfWickets": [{ "batterName": "…", "runs": 24, "wicketNumber": 1, "over": 7.2 }],
      "partnerships": [
        { "firstBatterName": "…", "firstBatterRuns": 19, "secondBatterName": "…", "secondBatterRuns": 4, "runs": 24, "balls": 44 }
      ]
    }
  ]
}
```

**Its own request, not a field on the match, and the reason is cost.** This comes from a second
source with an allowance of two hundred requests a *month*, so a client must treat it as
expensive: fetch it when a reader asks to see a scorecard, not when a match page opens. The
reference client puts it behind a button for exactly this reason.

**Returns 404 far more often than it returns a card, and that is normal.** Four separate
situations all produce one: the source is switched off, which is how this ships; the match could
not be paired with the source's own copy of it; the monthly allowance is spent; the source did not
answer. They are not distinguished, because a client does the same thing with all four — it shows
no scorecard. Distinguishing them would mean telling callers about our request budget, which is
our problem.

**The figures that are strings are strings on purpose.** `overs`, `strikeRate` and `economy` are
computed upstream and passed through. `overs: "22.4"` is twenty-two overs and four balls, not
22.4 overs, so arithmetic on it is wrong; and a strike rate for someone who has faced no balls has
no numeric answer to invent. Display them, do not compute with them. `innings[].overs` is a number
because it is a count of completed overs to one decimal, in the same notation — also not for
arithmetic.

`dismissal` is the source's own wording (`"c Konstas b Doggett"`, `"not out"`, or empty for
someone yet to bat) and is not parsed, because the wording is the information.

`isComplete` decides how long the answer is cached: a finished card cannot change and is held for
a day, a live one for five minutes. Five minutes is slow for live sport and is a budget decision
rather than a technical one — see [D-027](./decisions.md).

---

## Series

Series come from **two sources with two jobs**. The provider's `series` index says which series
exist; the matches we hold — in the live window and in our archive — say what can be shown about
one. Reasoning in [D-033](./decisions.md), which reverses the earlier decision to ignore the index
entirely while keeping its measurements: `endDate` has still never arrived as an ISO date, and no
standings or squads come from there either. It is read for an id, a name, a start date and a match
total, and for nothing else.

The practical consequence is that a series is **listed** whether or not we hold any of it, and the
response says which of those two situations you are looking at.

### `GET /api/series`

Every series the index lists within the pages we read, plus every series we hold a match of.

Ordered by whether a match is in progress, then whether we hold anything of it, then by **distance
from today in either direction** — so the current week is at the top and the list falls away into
both the past and the future. Sorting purely by date put a tour a year out at the top; we cannot do
better than distance because the provider gives us no end dates.

```json
{
  "success": true,
  "data": [
    {
      "id": "702ce6cb-a551-4aab-961e-0ed1548a3c74",
      "slug": "west-indies-tour-of-india-2026-702ce6cb-a551-4aab-961e-0ed1548a3c74",
      "name": "West Indies tour of India, 2026",
      "startTimeUtc": "2026-09-27T08:30:00+00:00",
      "lastMatchUtc": "2026-09-30T08:30:00+00:00",
      "matchCount": 2,
      "totalMatchCount": 8,
      "isOngoing": false
    },
    {
      "id": "c1ca1a51-9f8a-4a4f-9d2e-6f7b4a0a3e21",
      "slug": "sheffield-shield-2026-27-c1ca1a51-9f8a-4a4f-9d2e-6f7b4a0a3e21",
      "name": "Sheffield Shield 2026-27",
      "startTimeUtc": "2026-10-07T00:00:00+00:00",
      "lastMatchUtc": null,
      "matchCount": 0,
      "totalMatchCount": 31,
      "isOngoing": false
    }
  ],
  "message": "Success"
}
```

**The two counts are not interchangeable.** `matchCount` is how many matches of this series we hold
a score for; `totalMatchCount` is how many the series has, as the index counts them. A tournament
that began before this site started recording reports a small `matchCount` and a large
`totalMatchCount`, which is the difference between a narrow window and a short series.

`matchCount` is **not** the length of the `matches` array on the detail response, which also
includes the provider's unplayed fixtures. It is the subset of them that carries a score.

`totalMatchCount` is `null` when the index did not cover the series — anything outside the pages we
read. **`null` means "we were not told", never "none"**; a row claiming zero matches is dropped on
the way in, so zero never reaches a client.

`lastMatchUtc` is when the latest match we hold began, and `null` when we hold none. It is never
when the series ends, which we have no way of knowing. For a tour still being played it is a date
somewhere in the middle of the schedule, which is why the frontend only draws a closed date range
when `matchCount` has caught up with `totalMatchCount`.

`isOngoing` is only ever derived from held matches, so a series known solely from the index reports
`false` whatever its dates suggest. Deciding otherwise would mean comparing a start date against a
clock and publishing the result as a fact.

### `GET /api/series/{seriesId}`

Accepts either the bare id or the full slug.

```json
{
  "success": true,
  "data": {
    "series": { "...": "as above" },
    "matches": ["...match objects, in playing order..."],
    "standings": []
  },
  "message": "Success"
}
```

Returns **404** when the identifier is not a GUID, or when **neither** the index lists it nor we
hold a match of it. That second condition used to be just "no match we hold", which made every
index-only series on the list a dead link — listing something and then refusing to open it is worse
than not listing it.

**`matches` merges three sources**, keyed by match id, each overriding the last:

| Source | Contributes | Beaten by |
| --- | --- | --- |
| `series_info` fixtures | the full schedule, with venues and dates, all reading as unplayed | both of the below |
| our archive | scores for matches played since this site started | the live window |
| the live window | the current state of anything in progress | — |

So a match we hold a score for keeps its score, and one we do not still appears with its venue and
its start time rather than being omitted. Ordered by start time, which for a schedule is playing
order.

One provider call per series, cached for a few hours, spent only when a series is actually opened.
If that call fails or the series has no published schedule, `matches` falls back to what we hold
and the response stays a 200 — a thinner page, not an error.

`matchCount` and `totalMatchCount` here are the same values the list gives for the same series.
They are filled in from the index on this path too, specifically so a card reading "8 matches"
cannot open a page reading "2 matches".

**`standings` is empty unless a source supplied a table, which is the normal case.** A bilateral
tour has no points table at all, and the only source that publishes one for the tournaments that
do is read behind a switch that is off by default — see [D-020](./decisions.md) for what that
switch means and why it exists. Empty means *no table available*, never *this series has no
table*; those are different claims and only the first is ours to make, so render absence as no
section rather than as an empty table.

A standings row is published exactly as its source wrote it. Nothing is computed:

```json
{
  "group": "Elite Group A",
  "teamName": "MUM",
  "played": 5, "won": 3, "lost": 1, "tied": 0, "noResult": 1,
  "points": 16,
  "netRunRate": "0.512"
}
```

`group` is empty for a competition with one table. `teamName` is whatever the table printed,
usually an abbreviation, and is not expanded into a full name because that expansion would be a
guess. `netRunRate` is a string: it is signed and published to three places, and is shown as
given rather than reformatted.

---

## Teams

Teams are **assembled from the matches we hold**, like series, and for a stronger reason: the
provider issues no team identifier and has no team endpoint. A team is only ever what its matches
say about it. Reasoning in [D-023](./decisions.md).

A team's **slug is its identifier** — there is no id to pass instead.

### `GET /api/teams`

Every side appearing in a match we hold. Teams with a match in progress first, then most recently
seen.

```json
{
  "success": true,
  "data": [
    {
      "id": "india",
      "name": "India",
      "shortName": "IND",
      "logoUrl": "/api/crests/aHR0cHM6Ly9nLmNyaWNhcGkuY29tL2lhcGkvMzEt…",
      "matchCount": 1,
      "firstMatchUtc": "2026-09-27T08:30:00+00:00",
      "lastMatchUtc": "2026-09-27T08:30:00+00:00",
      "isActive": false
    }
  ],
  "message": "Success"
}
```

`matchCount` is **how many matches of this team we can show**, not how many it has played — the
same caveat a series carries. `logoUrl` is `null` for most sides, and is a path on this API rather
than a whole address — see [Crests](#crests). `shortName` falls back to the full name when no match
supplied an abbreviation; it is never invented from the name, because a made-up three-letter code
reads as authoritatively as a real one.

### `GET /api/teams/{teamId}`

Takes the slug, such as `india`.

```json
{
  "success": true,
  "data": {
    "team": { "...": "as above" },
    "matches": ["...match objects, in playing order..."],
    "series": [
      {
        "id": "702ce6cb-a551-4aab-961e-0ed1548a3c74",
        "slug": "west-indies-tour-of-india-2026-702ce6cb-…",
        "name": "West Indies tour of India, 2026",
        "matchCount": 1
      }
    ],
    "opponents": [{ "id": "west-indies", "name": "West Indies", "matchCount": 1 }],
    "formats": [{ "format": "ODI", "matchCount": 1 }]
  },
  "message": "Success"
}
```

Returns **404** when we hold no match for the slug. A side we have nothing of cannot be told apart
from one that never played.

**There is no won-lost record here, and its absence is deliberate.** The provider states results
only as prose — `"India won by 8 wkts"` — so a record would have to be parsed out of a sentence and
then published as a statistic. `formats` and `opponents` come from mapped fields and are facts;
`opponents` counts *meetings*, not a head-to-head. See [D-023](./decisions.md).

---

## Search

### `GET /api/search?q=`

Matches, teams and series whose names contain the term.

```json
{
  "success": true,
  "data": {
    "query": "ind",
    "matches": [{ "id": "india-vs-west-indies-abc123", "title": "1st ODI", "subtitle": "West Indies tour of India, 2026" }],
    "teams": [{ "id": "india", "title": "India", "subtitle": "1 match held" }],
    "series": [{ "id": "west-indies-tour-of-india-2026-702ce6cb-…", "title": "West Indies tour of India, 2026", "subtitle": "1 match held" }],
    "total": 3
  },
  "message": "Success"
}
```

Each hit's `id` is what that kind of thing is addressed by: a match slug, a team slug, a series
slug. Up to 10 per group.

A term shorter than **2 characters** returns an empty result with `200`, not a `400`. Someone
typing into a box is not making a mistake, and an error response would make the UI report one.

Results are **grouped rather than interleaved**, because a team and a match are not more or less
relevant than each other and combining them would need a scoring rule invented for the purpose.
Within a group, a title starting with the term sorts above one merely containing it; that is the
whole of the ranking.

**There is no `players` group.** No source available to us links a player to a match, and the
provider's player index holds a name and a country and nothing else, so a player result would lead
to a page with nothing on it. Evidence in [D-023](./decisions.md).

Matching is a substring scan in memory over the window plus the archive, not a text index — a
sizing decision, since the window has to be fetched anyway and the set is hundreds of rows. If the
archive grows enough for that to matter, `SearchService` is the seam to replace.

---

## Crests

### `GET /api/crests/{token}`

Serves a team crest as image bytes. The **one route that does not return the envelope** — its
caller is an `<img>`, which has no use for JSON — and the one that returns a bare 404 with no body.

```http
GET /api/crests/aHR0cHM6Ly9nLmNyaWNhcGkuY29tL2lhcGkvMzEt…

200 OK
Content-Type: image/jpeg
Cache-Control: public, max-age=604800, immutable
```

**Clients never construct these URLs.** The token arrives inside a response, as the `logoUrl` of
any team, and is opaque. Resolve the path against the API's base URL and pass it to an `<img>`
unchanged.

**Why it exists.** CricketData's terms forbid hot-linking the images they serve and name domain
blacklisting as the consequence, so the browser must not fetch them directly. This route fetches
each crest once, holds it in memory for a week, and returns it with an immutable cache header so a
reader asks for it once and never again. Reasoning in [D-030](./decisions.md).

**Why it is not a proxy you can point anywhere.** The address is decoded from the token and checked
against a two-host allow-list, both when the token is written and again when it is read. Anything
else — an unknown host, a plain-HTTP address, a token that does not decode, a response that is not
an image or is implausibly large — is a 404, and the UI falls back to the side's initials exactly as
it does for the many teams that have no crest at all.

Counted against its own rate-limit bucket at a much higher ceiling than the rest of the API, because
a list page asks for two crests per card and they cost us a memory lookup.

---

## Live stream

```http
GET /api/matches/{matchId}/stream
```

Server-Sent Events for one match. Accepts the same identifier as `GET /api/matches/{matchId}` —
either the slug or the bare id. Returns `404` with an empty body when the match is unknown, which
happens before any streaming begins so a client can tell the two cases apart.

**This endpoint does not use the response envelope.** The body is a sequence of frames, not a JSON
document, so `ApiResponse<T>` has nowhere to live. It is the only endpoint in the API like this.

| Event | Payload | When |
| --- | --- | --- |
| `match` | the same `MatchDetails` object `GET /api/matches/{matchId}` returns | once on connect, then on every change |
| `end` | `{ "reason": "completed" }` | the match is over; the client should close and not reconnect |
| *(comment)* | `: keepalive` | every 20s of silence, so proxies do not close an idle response |

```text
event: match
data: {"id":"90ae280c-…","status":"live","home":{…},"away":{…},"statusText":"India need 5 runs"}

: keepalive

event: end
data: {"reason":"completed"}
```

A frame is sent only when something a watcher would notice has changed — a run, a wicket, a ball,
the result sentence, or a new innings. Cosmetic differences in the upstream response do not
produce one.

### What this does and does not promise

Connecting costs the server nothing upstream. One provider call serves every connected client,
which is the reason the endpoint exists. But the underlying data still refreshes about every five
minutes, because the free plan allows a hundred calls a day and the provider states its free data
runs a few minutes behind play regardless. **A client must not present this as ball-by-ball.**

The server only polls the provider while at least one client is connected, so the browser is
expected to close its stream when the tab is hidden. `useMatchLiveStream` does this.

---

## Will not be built

These are specified in `project-plan.md` and a client might reasonably expect them. They are listed
here so nobody implements against a guess, or waits for something that is not coming.

**`GET /api/matches/{matchId}/scorecard` has since been built.** It is documented above. The
paragraph that used to stand here said it would not be; that was true of our main provider and
stopped being true of the project when a second source was measured. See
[D-027](./decisions.md).

**`/commentary` and `/stats` are still not built.** The same second source serves both — the
measurement found ball-by-ball text and over summaries — so they are buildable rather than
impossible. What stops them is arithmetic: commentary is only worth having if it keeps up, and
keeping up means a request every few deliveries, which the free allowance cannot pay for. The
scorecard fits because one request answers a whole innings. Reasoning in
[D-024](./decisions.md), the measurement in [D-026](./decisions.md).

A client should not wait for these two. But nobody should conclude from their absence that the
data does not exist.

`GET /api/series…`, `/api/teams…` and `GET /api/search` are now implemented and documented above.

**`GET /api/players…` will not be built.** Not deferred — there is no source. The provider's
`players_info` returns `id`, `name`, `country` and an image with no statistics of any kind, and
`series_squad` returns an empty array, so a player can be linked neither to a team nor to a match.
Evidence in [D-023](./decisions.md). A client should not wait for it.

---

## `currentBatters` on a match detail

`GET /api/matches/{matchId}` and the `match` stream event both carry `currentBatters`, an array of
`{ name, runs, balls }`. `balls` is a count of whole deliveries, so unlike `overs` it is a number
you may do arithmetic on.

**It is almost always empty, and empty means "we do not know".** It comes from a supplementary
source that is disabled by default and only knows matches an operator has mapped by hand, so a
client must render an empty array as absence — never as "nobody is batting", which is a different
claim. See [D-015](./decisions.md).

---

`features/matches/types.ts` mirrors the shapes documented here and is the frontend's only match
contract. Sprint 4 reconciled the two: `logoUrl` and `number` were added, `hasBallByBall` and
`hasSquads` replaced `tossText`, `summary`, `currentBatters` and `currentBowler`, and `OTHER`
joined the format union. Anything added to a DTO belongs in both places or in neither.
