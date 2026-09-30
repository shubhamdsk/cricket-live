# Deployment

Covers Sprint 8 tasks `8.22`–`8.28`.

**The API runs on Fly.io and the frontend on Vercel.** Fly was chosen for one reason: the archive
needs a disk that survives a redeploy, and free tiers that offer one are rare — Render's free web
services cannot attach a disk at all, which would mean losing the match history on every ship.

The general sections below still avoid naming a host, because the four failure modes they
describe are not Fly's or Vercel's. The host-specific steps are at the end.

Nothing in this document has been run against a real host. It is derived from the code and from
how these platforms behave, and it should be treated as a checklist to verify rather than a
transcript of something that worked. Fly's free allowance in particular has changed more than
once — check what it is today before assuming this costs nothing.

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
| `ForwardedHeaders__Enabled` | API | behind a proxy | Already `true` in `fly.toml`; see below |
| `VITE_API_BASE_URL` | frontend **build** | **yes** | Baked in; changing it means rebuilding |

The double underscore is how .NET maps an environment variable onto a nested configuration key.

Store both provider keys as the host's secrets, not as plain environment variables in a dashboard
that logs them. Neither key is detectable by GitHub's secret scanning — one is a bare GUID and the
other an opaque string, so neither matches a provider pattern and a clean alert list proves
nothing.

## 5. Forwarded headers, and the rate limiter that would quietly stop protecting anything

The container serves plain HTTP on 8080 and carries no certificate; the platform terminates TLS
at its edge and forwards plain HTTP inwards. Set `ForwardedHeaders__Enabled=true` when that is
the arrangement. `fly.toml` already does.

**The reason is rate limiting.** Partitions are keyed on the remote address, which behind a proxy
is the proxy — for everybody. The limiter keeps working perfectly and becomes a single global cap
of 120 requests a minute shared by every visitor, which one enthusiastic reader could exhaust for
everyone. Nothing logs it and nothing looks broken.

### What was measured, and one thing this does *not* fix

A first draft of this section claimed the app would enter an infinite redirect loop without this
setting. **That was tested against a production-mode build and is false**, so it is corrected here
rather than left to mislead.

`UseHttpsRedirection` needs to know an HTTPS port to redirect to. It looks for one in
`HttpsRedirectionOptions`, in the configured URLs, and in the server's bound addresses. The
container binds `http://+:8080` and nothing else, so it finds none, logs *"Failed to determine
the https port for redirect"* once at startup, and then does nothing at all. A request arriving
over plain HTTP with `X-Forwarded-Proto: https` returned **200, no `Location` header**, with the
setting both off and on.

So there is no loop to fix here — the middleware is inert. It would matter on a host that
configures an HTTPS port, and the setting is the right answer there.

**HSTS is unverified.** `UseHsts` only emits the header when the request is HTTPS, which without
these headers it never appears to be. That reasoning is sound but could not be confirmed locally:
ASP.NET excludes `localhost` from HSTS by default, so the header is absent either way on this
machine. **Check for `Strict-Transport-Security` on the deployed API** and do not assume it is
being sent.

### Why it is opt-in

`X-Forwarded-For` and `X-Forwarded-Proto` are just headers a client can send. Believing them is
only safe when something trustworthy overwrites them first, and only a deployment knows whether
that is true. With the setting on, the app clears the known-proxy allow-list — platform proxies
have no stable addresses, so there is nothing to put on one — which means it will believe those
headers from anyone who can reach it directly.

**On Fly that is safe, because nobody can.** The internal port is not publicly routable; the only
path in is through Fly's proxy. On a host where the container is directly reachable, leave it
off: an attacker could otherwise rotate `X-Forwarded-For` and bypass rate limiting entirely,
which is worse than the problem being solved.

`ForwardLimit = 1` means only the nearest hop is trusted. Anything further left in the chain was
written by the client.

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

---

## Fly.io — the API

`fly.toml` lives at the repository root, because the Dockerfile builds from there so its context
includes the whole backend folder.

**Create the volume before the first deploy.** A machine that declares a mount with no volume to
satisfy it will not start.

```bash
fly launch --no-deploy --copy-config
fly volumes create cricket_data --size 1 --region sin
fly secrets set CricketData__ApiKey=...
fly secrets set Cors__AllowedOrigins__0=https://<your-app>.vercel.app
fly deploy
```

Change `primary_region` in `fly.toml` first, to wherever your readers are. **A volume is pinned
to its region**, and moving one later means creating a new one — which means losing the archive
it holds.

`fly secrets` rather than `[env]`, for anything that is a secret. Values in `fly.toml` are
committed to a public repository; secrets are encrypted and injected at run time.

Two settings in there are worth understanding rather than copying.

**It scales to zero.** An idle machine stops and the next request starts it, which costs a few
seconds of .NET cold start and saves paying for a process nobody is talking to. Nothing is lost:
the archive is written as requests are served rather than on a timer, and the live poller only
polls while a client is connected. Fly will not stop a machine holding an open connection, so a
live stream keeps its own machine alive for as long as someone is watching.

**Never scale past one machine.** SQLite takes one writer, the poller and the archive writer both
run in-process, and the provider call budget is counted per process — two machines would each
believe they held the whole daily allowance and quietly spend twice it. The single volume
enforces this today, since a second machine would find no volume to mount, but it is a
correctness constraint and not a happy accident.

Concurrency is limited by **connections rather than requests**, because a live stream is one
connection held open for the length of a match and a request-based limit would count it once and
then stop noticing it.

### Deploying from CI

`.github/workflows/deploy-api.yml` deploys on push to `master`, and only when something the image
contains has changed — a docs-only commit should not restart the service. It then asks the
deployed app for readiness from outside, rather than trusting the deploy command's own view.

It needs a `FLY_API_TOKEN` repository secret, scoped to this app rather than the whole account:

```bash
fly tokens create deploy -a cricket-live-api
```

## Vercel — the frontend

Set **Root Directory** to `frontend` in the project settings. `frontend/vercel.json` covers the
rest: Vite framework preset, `dist` output, immutable caching on hashed asset filenames and
`no-cache` on `index.html`, which is the pairing that lets a deploy take effect immediately
without re-downloading unchanged code.

Set `VITE_API_BASE_URL` to `https://cricket-live-api.fly.dev` as a **build-time** environment
variable. It is inlined into the bundle, so changing it requires a rebuild, not a restart.

**There is deliberately no SPA rewrite.** Routes live in the URL fragment (`/#/match/...`), which
is never sent to a server, so the only path Vercel ever serves is `/`. A catch-all rewrite would
turn every mistyped URL into a 200 serving the app — a soft 404, which is worse for both readers
and crawlers than the real one.

Once the Vercel domain exists, put it in the API's CORS list and redeploy the API:

```bash
fly secrets set Cors__AllowedOrigins__0=https://<your-app>.vercel.app
```

Preview deployments get their own generated domains, which will **not** be in that list and will
fail CORS. Either add them, or treat previews as build-only checks.

---

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
