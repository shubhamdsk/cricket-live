# Deployment

Covers Sprint 8 tasks `8.22`–`8.28`.

**The API runs on Render, the archive in Neon PostgreSQL, and the frontend on Vercel.** All three
are free and none asks for a card.

That combination is a consequence of one constraint. The archive is the only thing here that
cannot be rebuilt, and no free host will keep a file: Render's free web services cannot attach a
disk at all, and the container is rebuilt from the image every time the service wakes from a
spin-down, not merely on deploy. Fly.io can keep a file but wants a card. So the archive moved
off the host's filesystem and into a managed database — [D-028](./decisions.md).

The general sections below avoid naming a host, because the failure modes they describe are not
any particular host's. The host-specific steps are at the end.

Nothing in this document has been run against a real host. It is derived from the code and from
how these platforms behave, and it should be treated as a checklist to verify rather than a
transcript of something that worked. Free allowances change — check what they are today before
assuming this costs nothing.

---

## The shape of it

Two deployables, and they are not symmetric.

**The frontend** is static files. `npm run build` produces `dist/`, and any static host serves it.
It talks to exactly one thing: our API.

**The API** is a container. It talks to CricketData, optionally to the RapidAPI Cricbuzz listing,
and writes to a PostgreSQL database that is not part of the container.

The browser never talks to a cricket provider. That rule is what keeps the provider keys on the
server, and it is not negotiable — see [security.md](./security.md).

---

## 1. The archive must live outside the container, and forgetting looks like nothing is wrong

**This is the one that will cost you real data.**

The archive is not a cache. It is the only record of matches that have fallen out of the
provider's few-day window, and it cannot be backfilled — CricketData does not serve history on
the free plan. Data lost here is lost permanently. This is what Sprint 7 was for.

Set `ConnectionStrings__Archive` to a PostgreSQL database. **If you do not, nothing fails**: the
app falls back to a SQLite file inside the container, `MigrateArchiveAsync` creates a fresh
schema, and the site looks like a new install that has not seen any cricket yet. You will not
notice on the first deploy, because it is empty then anyway.

On a free host that fallback is worse than it sounds. The container is rebuilt from the image
when the service wakes from a spin-down, not only when you ship, so the history would reset
several times a day rather than once a release.

Either connection-string format works:

```
ConnectionStrings__Archive=postgresql://user:password@host/dbname
ConnectionStrings__Archive=Host=host;Database=dbname;Username=user;Password=...;SSL Mode=Require
```

The first is what every managed host hands you, and Npgsql cannot parse it — the app converts it.
That conversion is worth knowing about because of *how* Npgsql fails without it: it throws, and
**the exception message contains the whole connection string, password included**, so a failed
deploy writes the database password into the log. If you ever see that, treat the password as
disclosed and reset it.

The provider is chosen by the shape of the string — anything that is not a `Data Source=` path is
treated as PostgreSQL. There is no separate provider setting, deliberately: two facts that can
contradict each other is one more way for a deploy to fail.

This supersedes the SQLite half of [D-014](./decisions.md); see [D-028](./decisions.md). SQLite
is still what local runs and the tests use.

Two consequences worth knowing before you scale:

- **One instance only, still.** The reason is no longer the database. The live poller and the
  provider call budget both live in the process, and `CricbuzzApiBudget` counts a monthly
  allowance per process — two instances would each believe they held all of it and quietly spend
  twice. Startup migration is also not safe with two instances racing it.
- **Free PostgreSQL suspends when idle.** The app enables EF's retry-on-failure for exactly this:
  the pool hands out a connection the server has already dropped, the first query fails, and the
  retry lands after the database has woken.

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
| `ConnectionStrings__Archive` | API | **in practice yes** | Not required to start, but without it the archive is a file in a container that does not keep files |
| `ASPNETCORE_URLS` | API | no | Already `http://+:8080`; override if the host insists on `$PORT` |
| `CricbuzzApi__Enabled` | API | no | Default off. Read [D-027](./decisions.md) first |
| `CricbuzzApi__ApiKey` | API | only if enabled | |
| `ForwardedHeaders__Enabled` | API | behind a proxy | Already `true` in `render.yaml`; see below |
| `VITE_API_BASE_URL` | frontend **build** | **yes** | Baked in; changing it means rebuilding |

The double underscore is how .NET maps an environment variable onto a nested configuration key.

Store the provider keys and the connection string as the host's secrets, not as plain environment
variables in a dashboard that logs them. Neither provider key is detectable by GitHub's secret
scanning — one is a bare GUID and the other an opaque string, so neither matches a provider
pattern and a clean alert list proves nothing.

In `render.yaml` all three are marked `sync: false`, which means the Blueprint declares that they
exist without carrying their values. That is what keeps them out of a public repository.

## 5. Forwarded headers, and the rate limiter that would quietly stop protecting anything

The container serves plain HTTP on 8080 and carries no certificate; the platform terminates TLS
at its edge and forwards plain HTTP inwards. Set `ForwardedHeaders__Enabled=true` when that is
the arrangement. `render.yaml` already does.

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

**On Render that is safe, because nobody can.** A free web service is only reachable through
Render's edge; the container has no publicly routable address of its own. On a host where the
container *is* directly reachable, leave it off: an attacker could otherwise rotate
`X-Forwarded-For` and bypass rate limiting entirely, which is worse than the problem being
solved.

`ForwardLimit = 1` means only the nearest hop is trusted. Anything further left in the chain was
written by the client.

## Provider terms

Read in full and recorded in [D-030](./decisions.md). Three things bind a deployment.

**Attribution is not required.** The footer credits CricketData with a link and that stays, but no
clause demands it. Nothing to satisfy here.

**Crests must not be hot-linked.** Their terms forbid it and name domain blacklisting as the
penalty, which would take the API down with the images. Crests are therefore served from
`/api/crests/...` and never as a provider address — see `CrestUrl`. **Nothing in a DTO should ever
carry an image URL on someone else's host again.** The cache is in memory, so a restart re-fetches
a handful of small files; a host with a disk or a CDN in front of it should improve on that.

**The free licence is personal and non-commercial**, and "any use for which you receive any
remuneration, whether in money or otherwise" counts as commercial. Advertising, donations or any
paid use requires buying credits first. This is a licensing decision before it is a product one.

## The Cricbuzz scorecard source

Reviewed in [D-031](./decisions.md), and the review did not come out in its favour. **Neither route
to that data is licensed.** Cricbuzz grants the site "for non-commercial use only and private
viewing only" and forbids communicating Materials to the public; their `robots.txt` disallows every
agent it has not named, and ours is not named. The RapidAPI route supplies no permission either —
RapidAPI's terms put the licence squarely between the consumer and the listing's publisher, who is
not Cricbuzz.

**The recommendation is to leave `CricbuzzApi__Enabled` unset in production.** A public site fails
the "private viewing only" half of the grant no matter how little it polls.

Three switches now govern every path to `www.cricbuzz.com`, and **all three are off by default**:

| Setting | What it turns on |
| --- | --- |
| `Cricbuzz__Enabled` | Reading a match page for the batters at the crease |
| `Cricbuzz__StandingsEnabled` | Reading a series page for a points table |
| `Cricbuzz__AutoResolve` | Reading the listing pages to pair a match automatically |

`Cricbuzz__AutoResolve` **used to default to `true`**, which meant setting `CricbuzzApi__Enabled`
alone was enough to start reading Cricbuzz's website — a permission from one party granted by a
switch named after another. If a deployment has the scorecard on and wants automatic pairing, it
must now say so explicitly. `Cricbuzz__MatchIds` pairs by hand and reads no page.

---

## Neon — the archive

Create a project, then take the **pooled** connection string from the dashboard. Paste it
unmodified; the app accepts the URL form.

There is no migration step to run by hand. `MigrateArchiveAsync` applies pending migrations at
startup, so the first successful deploy creates the schema, including the `case_insensitive`
collation that series filtering depends on.

Three limits on the free plan, and the obvious one is not the one that will stop you.

- **Storage, 0.5 GB per project.** Not a constraint here. A row is the match's scalar columns
  plus a JSON payload, on the order of a couple of kilobytes, so this is six figures of finished
  matches — decades of CricketData's coverage. This is an estimate from the schema rather than a
  measurement against a full archive.
- **Compute, 100 CU-hours per project per month.** This is the one to watch. At the 0.25 CU floor
  it is roughly 400 awake-hours against a 730-hour month. Comfortable for a site that sleeps,
  and it is consumed by the database being awake rather than by query volume.
- **Scale to zero after 5 minutes, and it cannot be disabled.** Hence retry-on-failure, above.

Exceeding storage blocks writes; exceeding compute suspends the database until the next billing
month. Both present as the archive failing while the rest of the site works, because live scores
come from the provider and never touch it. `/api/health/ready` reports the archive separately —
that is the endpoint that will tell you.

**Do not reuse `neondb_owner` for the application** if you are willing to spend ten minutes on
it. The app needs `SELECT`, `INSERT` and `UPDATE` on one table, not ownership of the database.

## Render — the API

`render.yaml` is at the repository root, because the Dockerfile builds from there so its context
includes the whole backend folder. Create the service with **New > Blueprint** and point it at
the repository; everything except the three `sync: false` secrets is applied from that file.

Set those three in the dashboard: `CricketData__ApiKey`, `Cors__AllowedOrigins__0`, and
`ConnectionStrings__Archive`.

Change `region` first, to wherever your readers are. Unlike a disk-backed deployment there is
nothing pinned to it now, so this is a latency choice rather than a permanent one — but the Neon
project has a region too, and the two should match or every query pays for the distance.

Render deploys on push to `master` by itself. **There is deliberately no deploy workflow in
`.github/workflows`** — one fewer credential to hold, and Render's own build is the same
Dockerfile CI would have used.

Two things about the free plan that will look like faults:

- **It sleeps after about 15 minutes idle**, and the next request pays a cold start for both the
  container and the Neon compute behind it. The first visitor after a quiet night waits.
- **The container is rebuilt from the image when it wakes.** In-memory caches start empty and
  `CricbuzzApiBudget` — which counts a *monthly* allowance in process memory — resets with it.
  It reconciles against the figure the gateway returns on the next call, so this self-corrects,
  but it is not a counter you can trust across a restart.

**Never scale past one instance.** The database no longer forces this, which makes it easier to
get wrong: the live poller and the call budget are per-process, and startup migration would race.

## Vercel — the frontend

Set **Root Directory** to `frontend` in the project settings. `frontend/vercel.json` covers the
rest: Vite framework preset, `dist` output, immutable caching on hashed asset filenames and
`no-cache` on `index.html`, which is the pairing that lets a deploy take effect immediately
without re-downloading unchanged code.

Set `VITE_API_BASE_URL` to `https://cricket-live-api.onrender.com` as a **build-time**
environment variable. It is inlined into the bundle, so changing it requires a rebuild, not a
restart.

**There is deliberately no SPA rewrite.** Routes live in the URL fragment (`/#/match/...`), which
is never sent to a server, so the only path Vercel ever serves is `/`. A catch-all rewrite would
turn every mistyped URL into a 200 serving the app — a soft 404, which is worse for both readers
and crawlers than the real one.

Once the Vercel domain exists, set `Cors__AllowedOrigins__0` to it in the Render dashboard.
Render restarts the service when an environment variable changes, so there is nothing else to
do — but it *is* a restart, so the caches and the in-process call budget start over.

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
7. Redeploy, then check `/api/matches/recent` still holds what it held before. This is the test
   that the archive is really in Neon and not in a file inside the container, and it is the only
   way to catch that before it costs you the history. Waiting for a spin-down and hitting the
   site cold tests the same thing and is closer to what will actually happen.
8. Check the Neon dashboard shows a non-empty `archived_matches` table. If step 7 passed but
   this is empty, the site is serving from cache and you have not tested anything yet.

### Measured: a genuine double cold start

Step 7 was passing only in its weaker form. Redeploys had preserved the archive many times, but
`.github/workflows/keep-warm.yml` pings `/api/health/live` every five minutes, so **Render had
never actually idled** and one path had never run: startup migrations against a Neon compute that
is itself suspended. `EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: 5s)` was the only
thing standing between that and a service that fails to boot.

Keep-warm was disabled, the site left alone for 43 minutes — past Render's ~15 minute idle and far
past Neon's 5 minute scale-to-zero — and then hit cold:

| Request | Result |
| --- | --- |
| `GET /api/health/ready` | `200 Healthy` in **26.3 s** |
| `GET /api/matches/recent` | 4.9 s, both archived ODIs present |
| `GET /api/crests/{token}` | `200 image/jpeg`, 1602 bytes, 582 ms |
| the same crest again | 130 ms, served from the now-warm memory cache |

The 26.3 s covers Render starting the container, the app booting, `MigrateArchiveAsync` reaching a
suspended Neon and waiting for it to wake, and then the readiness probe querying it. It boots, and
the retry policy is sufficient. Note what `Healthy` means here: readiness touches the database, so
a 200 from that endpoint is itself proof the migration step got through.

The archive came back with both matches and with crest paths already rewritten to `/api/crests/…`,
which also exercises the rewrite-on-read in `SqlMatchArchive` against rows written before that
code existed.

**Re-enable keep-warm afterwards.** It is the only reason a visitor does not pay the 26 s.
