# Cricket Live — Engineering Standards

These standards are binding for every sprint. Where this document and a habit disagree, this
document wins. Where this document and [`project-plan.md`](../project-plan.md) disagree on *what* to
build, the project plan wins; this document governs *how* we build it.

Related: [system design](./system-design.md) · [architecture](./architecture.md) ·
[frontend](./frontend.md) · [backend](./backend.md) · [security](./security.md) ·
[design system](./design-system.md) · [API](./api.md) · [sprint plan](./sprint-plan.md) ·
[decisions](./decisions.md)

This document states the rules. The per-half detail — folder layout, layering, naming, conventions
— lives in [frontend.md](./frontend.md) and [backend.md](./backend.md).

---

## 1. Development philosophy

This is a real product, not a demo. Code is expected to be maintainable, type-safe, accessible, and
reasonably performant.

Equally: **do not over-engineer.** Implement only what the current sprint requires. No technology
enters the codebase because it is popular; it enters because a concrete requirement in the current
sprint needs it. Every abstraction must name the problem it solves. Redis is not added before there
is live state to hold, and PostgreSQL is not added before there is something to persist.

When a requirement is genuinely uncertain, prefer the reversible choice.

---

## 2. Technology direction

| Layer | Choice |
| --- | --- |
| Frontend | React, TypeScript, Vite, Tailwind CSS, React Router, TanStack Query, Zustand, Lucide icons |
| Backend | ASP.NET Core Web API, C# |
| Database | PostgreSQL with EF Core |
| Cache and live state | Redis |
| Live updates | Server-Sent Events |
| Cricket data | CricketData (CricAPI), behind our own provider interface |

Additions not on this list require a recorded decision in [decisions.md](./decisions.md).

---

## 3. The provider is never the architecture

Every rule in this document bends around one fact: the cricket data comes from someone else, on
their schedule, in their shape, under their rate limit.

- **React never calls the provider.** The path is React → our API → the provider, always.
- **Provider-specific code stays in `Infrastructure/CricketData`.** Nothing above it knows the
  provider's name.
- **Provider response models are never exposed.** They are mapped to our own DTOs, and only our DTOs
  cross the wire to React.
- **We do not pretend to be faster than the provider.** Our update frequency is bounded by theirs.
  The UI says when data was last updated rather than implying it is live to the ball.
- **We only display what the provider actually returns.** A tab with no data behind it is hidden,
  not shown empty.

The free tier is a budget, not an unlimited resource. One live match must cost one poll regardless
of how many people are watching it.

---

## 4. Frontend structure

Feature-oriented, following the layout in [`project-plan.md`](../project-plan.md). The full layout,
layering, and naming conventions are in [frontend.md](./frontend.md).

```text
src/
├── app/            App, router, providers — no product logic
├── components/     common/ primitives, layout/ shell, match/ etc. presentational
├── features/       one folder per capability: api, components, hooks, types, utils
├── pages/          route targets
├── services/       transport: the API client
├── store/          Zustand stores
├── types/          shared types
├── utils/          pure helpers
└── styles/         Tailwind theme tokens
```

Folders are created when they earn their place. An empty folder is not organisation. A feature owns
its own components, hooks, transport, and rules; anything a second feature needs moves up at the
moment of the second use, not before.

---

## 5. React standards

Functional components and hooks only. Use the following when they solve a real problem, not to
demonstrate them:

- **Custom hooks** to keep data access and orchestration out of components
- **`useMemo` / `useCallback`** when a value feeds a dependency array or a memoised child, not by
  reflex
- **Lazy loading** at route boundaries, from Sprint 8
- **Error boundaries** around route content, so one failing screen cannot blank the app
- **Deliberate loading, empty, and error states** on every surface that can be slow, empty, or fail

Avoid large components, global state that could be local, `useEffect` for derived state, and prop
drilling across many layers.

---

## 6. Component architecture

Build reusable components. Buttons, cards, badges, tabs, match cards, score rows, and loading,
empty, and error states are defined once and reused. A pattern becomes a component the first time it
would be duplicated.

The intended separation:

```text
Component  →  Hook  →  API  →  services/apiClient
```

Components render and handle interaction. Hooks orchestrate: they run a query and shape what a
screen renders. The API layer is one typed function per endpoint. `services/` owns transport.

Formatting a score, deriving a match state, or deciding what a status line means is not a component's
job — it belongs in the feature's `utils/`, so two screens cannot disagree about what "142/3 (15.2)"
should look like.

---

## 7. State management

Four distinct kinds of state, kept distinct:

| Kind | Home |
| --- | --- |
| Component-specific: open/closed, active tab, toggles | React local state |
| Shared client state: navigation, preferences | Zustand |
| Server state | TanStack Query, one entry per resource |
| Live updates (from Sprint 5) | SSE, reconciled against the query cache |

Rules:

- Focused domain stores, never one god store. A store is created when a domain actually needs shared
  client state.
- Components subscribe through selectors so a change in one slice does not re-render the rest.
- Stores are fully typed. Actions live beside the state they change.
- **Server data is never copied into a Zustand store.** The cache is the one copy. An SSE message
  patches the cache; it does not open a second copy of the truth.

---

## 8. API architecture

Components never call `fetch`.

| Layer | Owns |
| --- | --- |
| `services/apiClient.ts` | base URL, envelope parsing, error normalisation into `ApiError` |
| `features/x/api/*.ts` | one function per endpoint, typed in and out, no rules |
| `features/x/hooks/*.ts` | query keys, cache lifetime, what a screen renders |

Every response is the standard envelope. `success` is the only thing that decides whether a call
worked; a call can succeed with `data: null` when there is legitimately nothing to return.

---

## 9. TypeScript

Strict mode stays on. `any` is not acceptable; use a real type, a union, a generic, or `unknown`
with narrowing. API responses, hooks, component props, and stores are all typed. Type safety is not
weakened to make a build pass — the build failing is the point.

---

## 10. Layout, viewport, and scrolling

Header and footer belong to the application layout, never re-created per page. The layout stays
stable across navigation; pages contribute only their own content.

- No horizontal scrolling at any supported width.
- No nested scroll containers without a reason.
- No fixed-height content that clips information.
- Tables become responsive rather than breaking the page. A scorecard reflows; it does not force the
  page sideways.

---

## 11. Responsive design

Mobile-first, and mobile is the primary target: cricket is watched on a phone. Every component works
at mobile, tablet, laptop, desktop, and large desktop. Responsive behaviour is implemented while the
component is written, never postponed to a later pass.

Controls adapt rather than disappear when the viewport shrinks.

---

## 12. UI and UX

The product should feel like a fast, modern cricket platform: clear score hierarchy, strong live
indicators, readable typography, touch-friendly controls, and no decoration that does not earn its
place.

Typography, colour, spacing, radius, and every shared component come from the
[design system](./design-system.md). Components never hardcode a colour. The same component type
looks the same on every screen.

Cricbuzz and CREX are product references, not visual ones. We keep our own identity.

---

## 13. Performance

Considered from the start, without blind optimisation. Efficient store selectors, small API
payloads, sensible query stale times, and caching at the layer that removes the most external calls.
Route-level code splitting and bundle analysis land in Sprint 8.

The performance number that matters most is external requests per live match per minute, not
milliseconds of render time.

---

## 14. Accessibility

Built in, not retrofitted: semantic HTML, keyboard reachability for the core loop, visible focus,
labels bound to controls, ARIA only where semantics fall short, sufficient contrast, and adequate
touch targets.

State is never communicated by colour alone. A live match is a red dot **and** the word "Live".

---

## 15. Security

There is no authentication in the MVP, which removes a whole class of risk and leaves a smaller,
sharper one: we are a public, unauthenticated API in front of a metered third-party provider. The
full picture is in [security.md](./security.md).

The short version:

- The provider API key lives in configuration, never in the repository and never in a response.
- All input is validated server-side and bounded.
- Errors are useful to people and useless to attackers: no stack traces or provider internals in
  responses.
- CORS is an allow-list, not a wildcard.
- Rate limiting and abuse protection land in Sprint 8, before anything is public.

---

## 16. Testing approach

The tests that exist stay and must keep passing. The suite is not expanded until Sprint 8 unless a
sprint explicitly calls for it — Sprint 3's mapper tests being the one planned exception, because a
mapper is exactly the kind of pure function that is cheap to test and expensive to get wrong.

Verification is otherwise manual and expected on every sprint:

1. Build both halves (`dotnet build`, `npm run build`) and lint the frontend
2. Run both and exercise the flow by hand
3. Check the browser console and the API logs for errors
4. Check layout at a few viewport sizes, including a narrow phone
5. Fix what is broken before the sprint is called done

---

## 17. Sprint workflow

```text
Understand sprint → inspect existing code → plan → implement sprint scope only
→ build and run → verify manually → fix → document decisions → stop
```

Sprints do not chain automatically. Work stops at the end of a sprint and waits for approval.
Anything listed under "Not in this sprint" waits, however small it looks.

Each sprint is a branch off `develop`, merged by pull request with the checklist in
`project-plan.md`. CI must be green before merge.

---

## 18. Documentation

Documentation lives in `docs/` and is updated when a decision or an interface changes, not for every
code change.

An established decision is not reversed silently. Changing one means recording why in
[decisions.md](./decisions.md).
