# Cricket Live — Security

Binding alongside [engineering-standards.md](./engineering-standards.md). It records the controls
in place today and the gaps we are carrying, and grows as each sprint lands.

Last updated: Sprint 2.

---

## 1. What kind of risk this product has

There is no authentication, no user accounts, and no personal data in the MVP. That removes most of
the usual attack surface and leaves a narrower, sharper problem:

**We are a public, unauthenticated API standing in front of a metered third-party service, holding
a credential that is ours.**

The realistic bad outcomes are, in order:

1. Our SportScore key is exposed and used by someone else.
2. Our API is hammered until the daily provider budget is exhausted, and the product goes dark for
   everyone.
3. Provider data or internal errors leak detail that helps someone attack the host.

Nothing here risks a person's data. Everything here risks availability and a bill.

---

## 2. Threat model

| Actor | Wants | Mitigated by |
| --- | --- | --- |
| Opportunistic scraper | Free cricket data through our API | Rate limiting (Sprint 8), caching so scraping costs us nothing extra |
| Someone who reads the repository | The provider key | Key never committed; configuration from the environment |
| Someone probing the host | Stack traces, versions, internal paths | Global exception handler returning a fixed message; security headers (Sprint 8) |
| A browser page on another origin | To call our API as a user | CORS allow-list |
| The provider itself | — | Treated as untrusted input: responses are mapped, never echoed |

---

## 3. Controls in place today

**Secrets.** No API key exists yet. When it arrives in Sprint 3 it comes from configuration and the
environment, never from the repository. `.gitignore` covers `.env`; the only committed environment
file is `frontend/.env.development`, which holds a localhost URL and nothing else.

**CORS.** An explicit allow-list bound from `Cors:AllowedOrigins`. Development allows the Vite dev
server; the default elsewhere is empty, so a misconfigured deployment fails closed rather than
allowing everyone.

**Error handling.** A global exception middleware logs the detail and returns a fixed
`"An unexpected error occurred."` in the standard envelope. No stack trace, no exception type, no
provider text reaches a caller.

**Transport.** HTTPS redirection is on outside Development ([D-008](./decisions.md)).

**Dependencies.** `npm audit` reports clean at the time of writing; the lockfile is committed.

---

## 4. Known gaps and when they close

| Gap | Risk | Closes |
| --- | --- | --- |
| No rate limiting | One client can exhaust the provider budget for everyone | Sprint 8 |
| No security headers | Clickjacking, sniffing, referrer leakage | Sprint 8 |
| No input validation layer | Malformed identifiers reach the provider call | Sprint 3, with the first parameterised endpoint |
| No request size limit | Trivially large bodies accepted | Sprint 8 |
| Provider key handling unproven | Key could be logged or echoed | Sprint 3 |
| No dependency scanning in CI | A vulnerable package lands unnoticed | Sprint 8 |

These are accepted for now because nothing is deployed and nothing is public. **None of them may
still be open when the application is first exposed to the internet.**

---

## 5. Rules for every sprint

- The provider key is never logged, never returned, and never placed in a URL where a proxy or log
  would keep it.
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

```text
[ ] Rate limiting in place and tested
[ ] Security headers set
[ ] CORS allow-list contains only real origins
[ ] Provider key supplied by the host, absent from the repository and from logs
[ ] Request size limits set
[ ] Health endpoint reports dependencies without leaking their addresses
[ ] Provider terms reviewed: usage limits, attribution, redistribution, commercial use
[ ] SportScore attribution visible in the UI
```

The last two are legal rather than technical, and they gate a launch just as firmly.
