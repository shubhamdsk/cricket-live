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

### `GET /api/matches/live` · `GET /api/matches/upcoming` · `GET /api/matches/recent`

Three lists, no parameters, each returning `data` as an array of matches. They are partitions of a
single upstream response, so asking for all three costs one provider call rather than three — see
[D-012](./decisions.md).

**Any of them can legitimately return an empty array.** The provider's current-matches window held
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

### `GET /api/matches/{matchId}`

Accepts either the bare id or the full slug. Returns one match with two extra fields:

```json
{ "hasBallByBall": false, "hasSquads": true }
```

These report what the provider claims to hold for this match rather than what we display. Across
every match observed during the spike `hasBallByBall` was `false`, which is why scorecard and
commentary are not yet buildable.

Returns **404** when the identifier is not a GUID, or is a well-formed GUID the provider does not
know. A malformed identifier is rejected without any provider call, which protects the daily
allowance as much as it validates the input.

---

## Not implemented yet

These are specified in `project-plan.md` and land in the sprint shown. They are listed so nobody
implements a client against a guess.

| Endpoint | Sprint |
| --- | --- |
| `GET /api/matches/{matchId}/stream` | 5 |
| `GET /api/matches/{matchId}/scorecard` | 6 |
| `GET /api/matches/{matchId}/commentary` | 6 |
| `GET /api/matches/{matchId}/stats` | 6 |
| `GET /api/series…`, `/api/teams…`, `/api/players…` | 7 |

`features/matches/types.ts` mirrors the shapes documented here and is the frontend's only match
contract. Sprint 4 reconciled the two: `logoUrl` and `number` were added, `hasBallByBall` and
`hasSquads` replaced `tossText`, `summary`, `currentBatters` and `currentBowler`, and `OTHER`
joined the format union. Anything added to a DTO belongs in both places or in neither.
