# Cricket Live — API

The implemented HTTP surface. Endpoints are added here as their sprint lands; the planned surface is
in [`project-plan.md`](../project-plan.md).

Base URL in development: `http://localhost:5140`.

Last updated: Sprint 2.

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
| 503 | The cricket provider is unreachable or refused us (Sprint 3) |

503 is the one worth stating early. When SportScore is down, this API is not broken and the caller
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

## Not implemented yet

These are specified in `project-plan.md` and land in the sprint shown. They are listed so nobody
implements a client against a guess.

| Endpoint | Sprint |
| --- | --- |
| `GET /api/matches/live` | 3 |
| `GET /api/matches/upcoming` | 3 |
| `GET /api/matches/recent` | 3 |
| `GET /api/matches/{matchId}` | 3 |
| `GET /api/matches/{matchId}/stream` | 5 |
| `GET /api/matches/{matchId}/scorecard` | 6 |
| `GET /api/matches/{matchId}/commentary` | 6 |
| `GET /api/matches/{matchId}/stats` | 6 |
| `GET /api/series…`, `/api/teams…`, `/api/players…` | 7 |

Until Sprint 3, the frontend serves these shapes from `features/matches/mocks/` through the real
function signatures ([D-009](./decisions.md)). The types in `features/matches/types.ts` are the
contract these endpoints are expected to meet.
