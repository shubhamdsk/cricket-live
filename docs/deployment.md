# Deployment

Covers Sprint 8 tasks `8.22`–`8.28`. Written before a host was chosen, so nothing here names one;
what it does instead is state the four things that will actually go wrong, because three of them
fail quietly.

Nothing in this document has been run against a real host. It is derived from the code and from
how these platforms behave, and it should be treated as a checklist to verify rather than a
transcript of something that worked.

---

## The shape of it

Two deployables, and they are not symmetric.

**The frontend** is static files. `npm run build` produces `dist/`, and any static host serves it.
It talks to exactly one thing: our API.

**The API** is a container. It talks to CricketData, optionally to the RapidAPI Cricbuzz listing,
and writes a SQLite file.

The browser never talks to a cricket provider. That rule is what keeps the provider keys on the
server, and it is not negotiable — see [security.md](./security.md).

---

## 1. The archive disappears on redeploy unless you mount a volume

**This is the one that will cost you real data, and it looks like nothing is wrong.**

The match archive is a SQLite file. Container filesystems do not survive a redeploy, so without a
persistent volume the database is recreated empty every time you ship. The app will not error —
`MigrateArchiveAsync` will happily create a fresh schema, `/api/matches/recent` will return an
empty page, and the site will look like a new install that has not seen any cricket yet.

You will not notice on the first deploy, because it is empty then anyway.

The archive is not a cache. It is the only record of matches that have fallen out of the
provider's few-day window, and it cannot be backfilled — CricketData does not serve history on the
free plan. Data lost here is lost permanently. This is what Sprint 7 was for.

So: **mount a volume at `/data`.** The image already points the connection string there:

```
ConnectionStrings__Archive=Data Source=/data/cricket-live.db
```

Every host spells this differently — a disk on Render, a volume on Fly, a persistent volume claim
on Kubernetes — but all of them need to be told, and none of them do it by default.

If the chosen host has no persistent disk on its free tier, that is the moment to revisit
[D-014](./decisions.md), which deferred PostgreSQL. The reasoning there was that SQLite is enough
for this workload, which is still true; what would have changed is that the *host* cannot keep a
file, which is a different argument entirely.

Two smaller consequences of SQLite worth knowing before you scale:

- **One instance only.** SQLite tolerates one writer, and the live poller and the archive writer
  both live in the process. Two instances would also each believe they hold the whole provider
  allowance — the same limitation as [D-014](./decisions.md) and `CricbuzzApiBudget`.
- Back the volume up, or accept that the history is only as durable as one disk.

## 2. The frontend build needs `VITE_API_BASE_URL`, and used to fail silently without it

Vite inlines `import.meta.env.VITE_*` at build time. An unset variable does not fail the build —
it becomes the literal string `undefined`, and every request goes to `undefined/api/...`.

`vite.config.ts` now refuses to build without it, so this is a loud error rather than a silent
one. Set it in the host's build environment:

```
VITE_API_BASE_URL=https://api.your-host.example
```

Absolute, with a scheme, and **no trailing slash** — it is joined to paths that already start with
one, and the build rejects both mistakes.

It is baked into the bundle, so **changing the API's URL means rebuilding the frontend**, not
restarting it.

**Never put a provider key in any `VITE_*` variable.** It would be inlined into the bundle and
served to every visitor.

## 3. CORS and the API URL have to agree, and a mistake looks like the backend being down

The API's allow-list is exact origins, never a wildcard, and it **fails at startup outside
development if the list is empty** — a deliberate choice, because an API that starts and then
refuses every browser is much harder to diagnose than one that does not start.

```
Cors__AllowedOrigins__0=https://your-frontend.example
```

Scheme and host must match the browser's origin exactly. `https://example.com` and
`https://www.example.com` are different origins. No trailing slash.

Get this wrong and the frontend shows connection errors on every page while the API's own health
endpoints return 200. Check the browser console before suspecting the backend.

## 4. SSE is the most likely thing to break, and it will look like staleness

`8.26`, flagged in the sprint plan as the biggest deployment risk.

`/api/matches/{id}/stream` holds a response open and writes to it for as long as the client is
connected. A surprising number of proxies, CDNs and platform edges will buffer that response
until it completes — which is never — or cut it after an idle timeout. Either way the browser
gets no events.

The failure is quiet. `useMatchLiveStream` falls back to polling, so scores still update, just
slowly. Nobody reports a bug; the site simply feels behind.

**Test it deliberately after the first deploy.** Open a live match, watch the network panel, and
confirm the stream stays open and receives frames. Do not conclude it works because the scores
change.

If it is buffered:

- Put the API on a subdomain that is not behind the CDN, or exclude `/api/matches/*/stream` from
  caching and buffering.
- On nginx-shaped proxies, `proxy_buffering off` and no read timeout on that path.
- The app already sets `X-Accel-Buffering: no`, which nginx honours and most platform edges
  ignore.

## Environment variables

| Variable | Where | Required | Notes |
| --- | --- | --- | --- |
| `CricketData__ApiKey` | API | **yes** | Startup fails without it |
| `Cors__AllowedOrigins__0` | API | **yes** | Exact frontend origin; startup fails if the list is empty |
| `ConnectionStrings__Archive` | API | no | Already `/data/cricket-live.db` in the image |
| `ASPNETCORE_URLS` | API | no | Already `http://+:8080`; override if the host insists on `$PORT` |
| `CricbuzzApi__Enabled` | API | no | Default off. Read [D-027](./decisions.md) first |
| `CricbuzzApi__ApiKey` | API | only if enabled | |
| `VITE_API_BASE_URL` | frontend **build** | **yes** | Baked in; changing it means rebuilding |

The double underscore is how .NET maps an environment variable onto a nested configuration key.

Store both provider keys as the host's secrets, not as plain environment variables in a dashboard
that logs them. Neither key is detectable by GitHub's secret scanning — one is a bare GUID and the
other an opaque string, so neither matches a provider pattern and a clean alert list proves
nothing.

## TLS

The container serves plain HTTP on 8080 and does not carry a certificate. Every host worth using
terminates TLS at its edge.

The app still calls `UseHttpsRedirection` and `UseHsts` outside development. If the host forwards
plain HTTP without setting `X-Forwarded-Proto`, that redirect can loop. Most platforms set it;
if you get a redirect loop on the first request, that is the cause, and the fix is
`UseForwardedHeaders` configured for the host's proxy — deliberately not added blind, because
trusting forwarded headers from an unknown proxy is its own problem.

## Attribution — `8.27`

The footer credits CricketData with a link, which is in place.

Before going public, **re-read the provider's terms** rather than assuming the footer satisfies
them. Free tiers commonly require specific wording, a specific link target, or attribution on
every page that shows their data rather than once in a footer. This has not been re-checked since
Sprint 3.

If the Cricbuzz scorecard source is enabled, that needs its own look. It is a reseller of a scrape
rather than a licensed feed ([D-027](./decisions.md)), so there may be nobody who can grant
permission and no correct attribution to give. Leaving it disabled in production is the
defensible choice until someone decides otherwise.

## Smoke test after the first deploy — `8.28`

In order, because each step assumes the one above it.

1. `GET /api/health/live` returns 200.
2. `GET /api/health/ready` returns 200. If it does not, read the body: it reports the archive and
   the request allowance separately and will say which.
3. `GET /api/matches/live` returns data. If it returns an empty list, check whether there is
   actually any cricket on before assuming a fault.
4. Load the frontend. No CORS errors in the console, and no requests to `undefined/...`.
5. Open a match page and confirm the innings render.
6. **Open a live match and watch the stream.** This is step 4 above and the one most likely to
   fail. Do not skip it because the scores updated.
7. Redeploy, then check `/api/matches/recent` still holds what it held before. This is the volume
   test, and it is the only way to catch a missing mount before it costs you the history.
