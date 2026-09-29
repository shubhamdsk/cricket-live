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
        "team": { "id": "india", "name": "India", "shortName": "IND", "logoUrl": "https://…" },
        "innings": [{ "number": 1, "runs": 300, "wickets": 2, "overs": "41.4" }]
      },
      "away": {
        "team": { "id": "west-indies", "name": "West Indies", "shortName": "WI", "logoUrl": "https://…" },
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
- `overs` is a **string** in cricket notation, where `41.4` means 41 overs and 4 balls. It is never
  arithmetic.
- `innings` is an array because a Test side bats twice. It is empty for a side that has not batted.
- `statusText` is the provider's own sentence. Display it verbatim; never paraphrase it.
- `logoUrl` is nullable. The provider only has images for teams it holds a profile for.
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
every match observed during the spike `hasBallByBall` was `false`, which is why scorecard and
commentary are not yet buildable.

Returns **404** when the identifier is not a GUID, or is a well-formed GUID that neither the
provider nor the archive knows. A malformed identifier is rejected without any provider call, which
protects the daily allowance as much as it validates the input.

A match the provider's window has dropped is still answered from the archive if we kept it, so a
link to a finished match does not rot the moment the window moves past it.

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

## Not implemented yet

These are specified in `project-plan.md` and land in the sprint shown. They are listed so nobody
implements a client against a guess.

| Endpoint | Sprint |
| --- | --- |
| `GET /api/matches/{matchId}/scorecard` | 6 |
| `GET /api/matches/{matchId}/commentary` | 6 |
| `GET /api/matches/{matchId}/stats` | 6 |
| `GET /api/series…`, `/api/teams…`, `/api/players…` | 7 |

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
