# Cricket Live — Security

Binding alongside [engineering-standards.md](./engineering-standards.md). It records the controls
in place today and the gaps we are carrying, and grows as each sprint lands.

Last updated: 5 October 2026, with the site live at
[cricket-live-shubhamdsk1.vercel.app](https://cricket-live-shubhamdsk1.vercel.app/).

## Reporting a vulnerability

Please do not open a public issue. Use **Report a vulnerability** on the repository's
[Security tab](https://github.com/shubhamdsk/cricket-live/security), which reaches the maintainer
privately. Include the URL or endpoint, what you sent, and what came back. A leaked credential is
treated as the most urgent case and is rotated first, before anything else is investigated.

---

## 1. What kind of risk this product has

There is no authentication, no user accounts, and no personal data in the MVP. That removes most of
the usual attack surface and leaves a narrower, sharper problem:

**We are a public, unauthenticated API standing in front of a metered third-party service, holding
a credential that is ours.**

The realistic bad outcomes are, in order:

1. Our CricketData key is exposed and used by someone else.
2. Our API is hammered until the daily provider budget is exhausted, and the product goes dark for
   everyone.
3. Provider data or internal errors leak detail that helps someone attack the host.

Nothing here risks a person's data. Everything here risks availability and a bill.

---

## 2. Threat model

| Actor | Wants | Mitigated by |
| --- | --- | --- |
| Opportunistic scraper | Free cricket data through our API | Per-IP rate limiting, caching so scraping costs us nothing extra |
| Someone who reads the repository | The provider keys | Keys never committed; configuration from the environment |
| Someone probing the host | Stack traces, versions, internal paths | Global exception handler returning a fixed message; security headers |
| A browser page on another origin | To call our API as a user | CORS allow-list |
| The provider itself | — | Treated as untrusted input: responses are mapped, never echoed |

---

## 3. Controls in place today

**Secrets.** The CricketData key comes from .NET user secrets in development and the
`CricketData__ApiKey` environment variable elsewhere. It is not in `appsettings.json`, and there is
deliberately no empty `"ApiKey": ""` placeholder there either — an empty slot in a tracked file is
an invitation to fill it in and commit it. A missing key fails options validation at startup with a
message pointing at the README, rather than surfacing as a 500 on the first request.

The optional scorecard source has a second key, `CricbuzzApi:ApiKey`, handled the same way and with
the same absent placeholder. It differs from the CricketData key in one respect worth noting: it
travels in an `x-rapidapi-key` **header** rather than a query string, so it never appears in a URL
and therefore cannot leak through anything that logs one. The CricketData key has no such option —
that provider only accepts it in the query string, which is why the rule there is to log the path
and never the URI.

`.gitignore` covers `.env` and the `.spike/` folder of captured provider responses; the only
committed environment file is `frontend/.env.development`, which holds a localhost URL and nothing
else.

**The provider key travels in the query string.** CricketData authenticates with `?apikey=`, not a
header, which is their design and not ours. Three things contain it:

- Requests are made only from the server. The browser never sees the key, because React only ever
  talks to our own API.
- .NET's `HttpClient` logging redacts query strings by default, so the log reads
  `GET https://api.cricapi.com/v1/currentMatches?*`. This was confirmed by inspecting real logs, not
  assumed.
- Nothing in our code logs the constructed URI. Only the path is ever logged.

**CricketData echoes the key back in every response body.** The first field of a `currentMatches`
payload is `"apikey":"<the key you sent>"`. This means a saved provider response is a secret-bearing
file, which is not obvious from looking at one — it reads as match data. Two consequences:

- `.spike/` is gitignored for this reason and not only to keep noise out of the repository. The
  captured responses in there do contain the live key.
- **Test fixtures are stripped of `apikey` before being committed.** The files under
  `backend/tests/CricketLive.Infrastructure.Tests/Fixtures/` have the field removed, and the mapper
  never reads it, so nothing depends on it being there. Any fixture captured in a later sprint gets
  the same treatment.

Verified on 2026-09-29 that no object anywhere in the repository's history contains the key, that
GitHub secret scanning reports no alerts, and that the only occurrences of the string `apikey` in
tracked files are the query-parameter name in `CricketDataClient` and this document.

**CORS.** An explicit allow-list bound from `Cors:AllowedOrigins`. Development allows the Vite dev
server; the default elsewhere is empty, so a misconfigured deployment fails closed rather than
allowing everyone.

**Error handling.** A global exception middleware logs the detail and returns a fixed message in the
standard envelope — `"An unexpected error occurred."` for anything unhandled, and a 503 saying
cricket data is temporarily unavailable when the provider fails. No stack trace, no exception type,
and no provider text reaches a caller: the provider's own reason strings are logged and discarded.

**Input validation.** A match identifier must be a GUID, or one of our slugs ending in one, before
any provider call is made. That is a budget control as much as a validation rule — without it a
stream of junk identifiers would spend a daily allowance that is only a hundred calls wide.

**Transport.** HTTPS redirection is on outside Development ([D-008](./decisions.md)).

**Rate limiting.** Per caller IP: 120 API requests a minute, 600 crest images a minute, and 4
concurrent live streams (`RateLimiting.cs`, configurable under `RateLimits`). Over the limit is a
429 in the standard envelope with `Retry-After`. Production sets `ForwardedHeaders__Enabled`, since
behind Render's proxy every visitor would otherwise share one bucket.

**Security headers.** The API sends a `default-src 'none'` Content-Security-Policy, `nosniff`,
`X-Frame-Options: DENY` and `Referrer-Policy: no-referrer`, hides the server header, and adds HSTS
in production (`SecurityHeadersMiddleware`). The website sets its own in `frontend/vercel.json`:
a Content-Security-Policy that allows only the site itself and the API origin, plus the theme
script in `index.html` by its sha256 hash; `nosniff`; `X-Frame-Options: DENY`;
`Referrer-Policy: strict-origin-when-cross-origin`; and a `Permissions-Policy` that turns off camera,
microphone, location, payment and USB. Changing the API host, or editing that inline script, means
updating the policy.

**Dependencies.** `npm audit` reports clean at the time of writing; the lockfile is committed.

---

## 4. Known gaps and when they close

| Gap | Risk | Closes |
| --- | --- | --- |
| ~~No rate limiting~~ | One client can exhaust the provider budget for everyone | **Closed**, see §3 |
| ~~No security headers~~ | Clickjacking, sniffing, referrer leakage | **Closed** for the API and the website, see §3 |
| ~~No request size limit~~ | Trivially large bodies accepted | **Closed by design**: the API has no write endpoints and never reads a body; Kestrel's default cap still applies |
| ~~No dependency scanning~~ | A vulnerable package lands unnoticed | **Closed**: Dependabot alerts and security fixes are on, with weekly grouped updates for npm, NuGet and Actions (`.github/dependabot.yml`) |
| Key not in a GitHub Actions secret | — none today | Not needed, see below |

The last row is a deliberate non-action rather than an oversight. No workflow needs a provider
key: the mapper tests run against committed fixtures, CI never calls a provider, and deployment is
done by Vercel and Render reading their own environment. Storing a credential that nothing
consumes adds a place for it to leak without removing one.

**The repository is public.** That raises the cost of a leaked secret from "rotate it quietly" to
"assume it was harvested within minutes", which is why the scan below is a merge gate rather than
advice.

**It was advice, for eight sprints, while this document called it a gate.** The workflow ran on
every push and pull request and reported correctly; neither `master` nor `develop` had any branch
protection, so nothing required the result and a failing scan would not have stopped a merge. Both
branches now require the `scan` check along with `backend` and `frontend`. The reasoning, and the
more general bug this is the second instance of today, is in [D-032](./decisions.md#d-032) — the
short version being that a scan which scans is not the same thing as a merge which was refused,
and only one of those is observable from the pull request page.

**A CI secret scan runs on every push and pull request** (`.github/workflows/secrets.yml`). It
looks for the two shapes a CricketData key can take — `"apikey":"<guid>"` and `apikey=<guid>` —
across tracked files only. It deliberately does not look for bare GUIDs: match ids are GUIDs and
fill this repository legitimately, so a generic secret scanner would either drown in false
positives or be tuned until it caught nothing. GitHub's own secret scanning cannot help here for
the same reason, and reports no alerts precisely because a bare GUID matches no known provider
pattern. Absence of an alert from it is not evidence of absence of a key.

---

## 5. Rules for every sprint

- The provider key is never logged, never returned to a caller, and never reaches the browser. It
  does travel in the provider's query string because that provider offers no alternative; see
  section 3 for what contains that.
- Provider responses are untrusted input: mapped into our DTOs, never passed through, and never
  echoed in an error message.
- Every route parameter is validated before it reaches a provider call or a database query.
- Errors are useful to people and useless to attackers.
- Caching is a security control here, not only a performance one: every cache hit is a provider
  call that an abusive client did not get to cause.
- Secrets come from the environment. A missing one should stop the application at startup, not
  surface as a 500 on the first request.

---

## 6. Before anything is public

The site is public, and every item here was settled before or at launch.

```text
[x] Rate limiting in place and tested
[x] Security headers set, on the API and on the website
[x] CORS allow-list contains only real origins (the Vercel site, via Cors__AllowedOrigins__0)
[x] Provider keys supplied by the host, absent from the repository and from logs
[x] Request size limits set (no endpoint accepts a body)
[x] Health endpoint reports dependencies without leaking their addresses
[x] Provider terms reviewed: usage limits, attribution, redistribution, commercial use
[x] CricketData attribution visible in the UI
```

The health endpoints answer with a bare status word. `/api/health/live` runs no checks at all,
which is why the external keep-awake pinger uses it: a ping never spends a provider call.

The last two are legal rather than technical, and they gate a launch just as firmly. Both are
settled in [D-030](./decisions.md): the terms are read, attribution turns out not to be required
and is given anyway, and the one clause the site was breaking — hot-linking the provider's images —
is fixed. Two conditions came out of it and are conditions rather than tasks, so they do not appear
above: **the site must not be monetised in any form while it runs on the free key**, and **no DTO
may carry an image URL on a host that is not ours**. The second has a guard in code; the first does
not and cannot.

The Cricbuzz sources had their own review in [D-031](./decisions.md), with the same outcome in a
harder form: **neither route to that data is licensed**. Production still enables
`CricbuzzApi__Enabled` for scorecard depth on the free CricketData plan; that does not resolve the
licence question and is recorded in [D-042](./decisions.md). Three switches now govern every path to
`www.cricbuzz.com` — `Cricbuzz__Enabled`, `Cricbuzz__StandingsEnabled` and `Cricbuzz__AutoResolve` —
and all three are off by default. Before they were split out, `AutoResolve` defaulted to `true`,
which meant a switch named after the RapidAPI gateway silently granted a permission belonging to
Cricbuzz; that is the class of bug worth looking for elsewhere, where one flag stands in for two
parties' consent. Production now turns `Cricbuzz__AutoResolve` on deliberately, in `render.yaml`,
so scorecards can be paired at all; see [D-043](./decisions.md).

The crest route deserves a line of its own here, since it is the only endpoint that makes an
outbound request on a caller's behalf. The address is never taken from the request: it is decoded
from an opaque token and checked against a two-host allow-list both when the token is written and
again when it is read, so the route cannot be used to reach an arbitrary address from inside the
deployment. Responses are accepted only when they carry an `image/*` content type and fall under a
size cap. Anything else becomes a 404 and the UI shows the side's initials.
