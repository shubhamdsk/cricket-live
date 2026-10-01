# Cricket Live — Frontend Standards

How `frontend/` is built. This document is the detail behind
[engineering standards §4–§14](./engineering-standards.md); where the two disagree, the standards
win and this document is corrected.

Stack: React 19, TypeScript (strict), Vite, Tailwind CSS v4, React Router, TanStack Query for server
data, Zustand for client state, Lucide for icons.

Related: [backend](./backend.md) · [design system](./design-system.md) · [API](./api.md) ·
[decisions](./decisions.md)

---

## 1. Structure

```text
src/
├── app/                        application shell; no product logic
│   ├── App.tsx                 providers + router, nothing else
│   ├── providers.tsx           QueryClient and any global surfaces
│   └── router.tsx              the route tree
├── components/
│   ├── common/                 primitives: Button, Card, Badge, Tabs, Skeleton, Spinner, EmptyState, ErrorState
│   ├── layout/                 AppLayout, Header, Footer, Container
│   └── match/                  presentational cricket pieces: MatchCard, MatchHeader, TeamScoreRow…
├── features/                   one folder per capability
│   └── matches/
│       ├── api/                one function per endpoint, no rules
│       ├── components/         data-bound pieces for this feature
│       ├── hooks/              query keys and what a screen renders
│       ├── types.ts            what the API returns for this feature
│       └── utils/              formatting and derivation
├── pages/                      route targets, one folder per page
├── services/                   apiClient: base URL, envelope, error normalisation
├── store/                      Zustand stores, one per domain
├── types/                      types shared across features
├── utils/                      pure helpers used by more than one feature
└── index.css                   Tailwind theme tokens
```

Rules:

- A folder exists because files live in it. Empty folders are not organisation.
- A feature owns its components, hooks, api, types, and rules. Something moves up to `components/`,
  `utils/`, or `types/` at the moment of the **second** use, not in anticipation of one.
- Nothing in `app/` contains product logic; nothing in `features/` decides routing or providers.
- `components/common/` never imports from `features/` or `store/`. Primitives take props.
- `components/match/` is presentational: it takes a match and renders it. It does not fetch.

### Naming

| Thing | Convention | Example |
| --- | --- | --- |
| Components | PascalCase file and export | `MatchCard.tsx` |
| Hooks | `use` prefix, camelCase file | `useMatches.ts` |
| Transport | verb-first function per endpoint | `getLiveMatches` |
| Route pages | `*Page.tsx`, named export | `HomePage.tsx` |
| Stores | `<domain>Store.ts` | `uiStore.ts` |

Imports always use the `@/` alias, never `../../`.

---

## 2. Layering

```text
Component  →  Hook  →  API  →  services/apiClient  →  .NET API
```

| Layer | Owns | Never does |
| --- | --- | --- |
| Component | rendering, local UI state, user events | fetch, formatting rules, business decisions |
| Hook | the query key, cache lifetime, what a screen renders | HTTP details, rendering |
| API | one function per endpoint, typed in and out | rules, retries, error mapping |
| `apiClient` | base URL, envelope parsing, error normalisation | anything feature-specific |

A query function passes the query's `signal` through, so a cancelled navigation cancels the request.
An error reaches the hook as the error that was thrown, so a page can check `ApiError.status` and
tell "this match does not exist" apart from "the API is down".

**Deriving display meaning belongs in the feature's `utils/`, not the component.** `formatTeamScore`
decides that a Test side's two innings read `341 & 88/2` and that ten wickets down is written `341`
rather than `341/10`. A component renders what it is handed.

### Mock data before an endpoint exists

A sprint that builds UI ahead of its endpoint puts fixtures in the feature's `mocks/` and keeps the
**real function signature** in `api/`:

```ts
export function getLiveMatches(signal?: AbortSignal): Promise<Match[]>
```

The mock resolves after a short delay and honours the abort signal, so loading states and
cancellation are exercised for real. When the endpoint lands, the body changes and nothing above it
does. The types in `features/x/types.ts` are the contract the backend then has to meet.

Sprint 2 built the match UI this way and Sprint 4 swapped the bodies, which is the pattern working.
What it cannot do is invent data: the contract held for scores, teams, venue, and status, but the
toss, editorial summary, and players at the crease were things we had assumed a provider would
carry. It does not. The fixtures are deleted along with the UI that depended on them.

**So the rule has a second half: a mock may stand in for data we have seen, not for data we hope
exists.** A field nobody has observed in a real response is a design sketch, and a design sketch
does not belong behind a function that claims to return the API's shape.

### When the data will not support the design

Remove the section. Not a placeholder, not an empty state, not "coming soon" — those all tell the
reader that something is missing, and a page that keeps apologising reads as broken. A match page
with a header, an innings breakdown, and match information is complete. The same page with four
tabs where three say "arrives in Sprint 6" is the same content wearing a sign that says it is not.

---

## 3. State

| Kind | Home |
| --- | --- |
| Component-specific: active tab, menu open | React local state |
| Shared client state: navigation, preferences | Zustand store |
| Server state | the TanStack Query cache, one entry per resource |
| Live updates (from Sprint 5) | SSE, reconciled against the cache |

Zustand rules:

- One store per domain, never a god store.
- Components subscribe with selectors: `useUiStore((state) => state.isMobileNavOpen)`.
- Actions live in the store beside the state they change, and the store is fully typed.
- Server data is never copied into a store. There is one copy, in the cache, which is also what
  makes two screens reading the same list one request.

### Server state

`app/providers.tsx` holds the one `QueryClient` and the default policy, so no screen sets it again.
A feature declares its keys in `<feature>/hooks/`, built from a single `matchKeys` object so an
invalidation cannot miss a key by typo.

Stale times are set per data type, not globally, because cricket data ages at very different rates:

| Data | Freshness |
| --- | --- |
| Live matches and live match detail | short; superseded by SSE once Sprint 5 lands |
| Upcoming fixtures | minutes |
| Completed matches, scorecards of finished games | long; a finished match does not change |
| Teams, players, series metadata | very long |

Nothing refetches because a window regained focus. Freshness on an open live screen comes from the
SSE stream, not from polling the API from every tab — the whole point of the architecture is that
one live match costs one provider poll.

### Live updates, from Sprint 5

`useMatchLiveStream()` owns the connection and handles connected, message, disconnected, reconnect,
and error. A message patches the query cache for that match; it does not become a second copy of the
data in a store. The stream closes on unmount and when the match ends.

A live screen states when it was last updated. We do not imply data is fresher than the provider
makes it.

---

## 4. Routing

All routing lives in `src/app/router.tsx`. Pages never decide routing.

```text
/                   Home
/live               Live matches
/matches            All matches, filtered by status, date and series
/match/:slug        Match details — Summary, Scorecard, Commentary, Stats
/series             Series and tournaments
/series/:slug       Series details — schedule and standings
/teams              Teams
/teams/:slug        Team details — fixtures and results
/search             Search, by `?q=`
/*                  Not found
```

**Routes are real paths** (`createBrowserRouter`). They were fragments until
[D-038](./decisions.md), which explains both why they changed — a fragment is never sent to a
server, so the whole site had one indexable address — and the two traps in the change.

The host must therefore rewrite unmatched paths to `index.html`; see `frontend/vercel.json`. Two
consequences worth knowing before you touch either file:

- Links shared in the old `/#/teams` form still work. `src/app/legacyHashRoute.ts` rewrites them
  on load, and it is called from `router.tsx` **above** `createBrowserRouter` rather than from
  `main.tsx`, because the router reads the location as its own module is evaluated.
- An unknown path now answers `200`, since a rewrite cannot know a path is wrong. That is why the
  not-found page sets `noindex`.

Write links as `<Link to="/matches">`, as before; no component should construct a URL by hand.

Matches are addressed by slug, not by provider id, so a URL survives a provider change and reads
like something a person would send to a friend.

**Filter state belongs in the URL, not in `useState`.** `useMatchFilters` reads and writes it
through `useSearchParams`. That is what makes a filtered view linkable, reloadable and reachable
with the back button.

Route-level lazy loading arrives in Sprint 8, through the router's own `lazy` option, so each screen
becomes its own chunk.

---

## 5. Layout, viewport, scrolling

```text
AppLayout
├── Header      sticky, navigation
├── Main        page content, one Container
└── Footer      attribution
```

- The shell fills the viewport; the footer sits below the content, not floating over it.
- No horizontal overflow at any supported width, verified with real data — long team names and
  four-digit Test totals, not `Team A`.
- No nested scroll containers without a reason. A scorecard reflows on a phone; it does not become a
  sideways-scrolling table inside a page.
- Header and footer are never re-created inside a page.

---

## 6. React practice

- Functional components and hooks only.
- `useMemo` / `useCallback` when a value feeds a dependency array or a memoised child, not by reflex.
- Data fetching lives in a hook that turns a query into what one screen renders. Components receive
  state, not promises.
- Every surface that can be slow, empty, or fail has a deliberate state. `Skeleton`, `Spinner`,
  `EmptyState`, and `ErrorState` exist so this is never improvised.
- A first load shows a skeleton shaped like the content that is coming, not a spinner in the middle
  of an empty page. Nothing should move when the data lands.
- A section that fails offers a retry where retrying can help.

---

## 7. TypeScript

Strict mode, no `any`. Feature types describe what the API returns. Props, hooks, and stores are all
typed. A failing build is the control working.

Cricket has shapes that a loose type will get wrong, so the types carry them: a side has an array of
innings because a Test side bats twice, and a match that has not started has an empty array rather
than a zero score.

---

## 8. Styling and icons

- Tailwind utilities with tokens from `index.css`. No hardcoded colour, radius, or shadow in a
  component.

**Two themes, and no `dark:` variant anywhere.** `index.css` defines the token names once; the
`@theme` block holds the dark values, because dark is the default, and `html.theme-light` restores
the light palette. Every utility resolves through `var()`, so one class on the root element reskins
the app and no component knows which theme is on. [D-039](./decisions.md) explains the shape and
[D-040](./decisions.md) the navy palette and the frosting.

**Anything that paints a surface also gets `glass`**, the one utility that carries the
`backdrop-filter`. Put it alongside `bg-surface`; it is inert in the light theme, where surfaces
are opaque and there is nothing to see through. Do not reach for `backdrop-blur-*` — the strength
of the effect is meant to be one number per theme, not a decision per component.

What this asks of you when adding a component:

- **Name the role, not the appearance.** `surface` is a glass panel and is translucent in dark;
  `surface-raised` is the opaque one, for anything that covers content rather than tinting it;
  `surface-sunken` is recessed, for table header strips; `chrome` is the header and footer, which
  content scrolls under.
- **`brand` is a fill, `brand-strong` is a text colour**, and they sit at opposite ends of the navy
  ramp in each theme, because a true navy is too dark to read as text on a dark page. Text on a
  brand fill is `text-on-brand`; the hover fill is `brand-hover`. Using `brand-strong` as a
  background, or `surface` as a text colour, works in light and breaks in dark — both mistakes were
  in the codebase and are described in D-039.
- **Check contrast against the background the colour actually lands on**, which for translucent
  surfaces is the panel over the page, not the page. `Badge`'s doc comment is the cautionary tale.
- Tailwind's built-in shadows (`shadow-sm`, `shadow-lg`) are tuned for light backgrounds and
  disappear on dark. Use `shadow-card` and `shadow-lift`.

- Icons come from `lucide-react`, sized with `size-*`, inheriting colour through `currentColor`, and
  `aria-hidden` where the surrounding control already carries the name. Raw `<svg>` is not pasted
  into components, and emoji are not interface icons.
- Status is never colour alone: the live badge is a red pulse **and** the word "Live".
- Scores use tabular figures, so a total does not jog sideways when it ticks from 99 to 100.
- Touch targets are at least 44px on mobile for anything in the primary navigation or a card action.

---

## 9. Accessibility

Non-negotiable per screen: semantic landmarks, labels bound to controls, visible focus, a keyboard
path through the core loop, and adequate contrast.

- Tabs are a real `tablist`: arrow keys and Home/End move between tabs, and each panel is bound to
  its tab with `aria-controls` and `aria-labelledby`.
- The mobile navigation toggle carries `aria-expanded` and `aria-controls`.
- An error region announces itself with `role="alert"`; a loading region uses `role="status"`.
- Score tables use `<th scope="col">` and a row header for the player, so a screen reader reads
  "Gill, runs 58" rather than five loose numbers.

---

## 10. Verification

Before calling work done:

```powershell
npm run lint          # oxlint, expected clean
npm run format:check  # prettier
npm run build         # tsc -b && vite build, expected clean
```

Then run it: every page, the browser console, a match that does not exist, a section that fails, and
the layout at a narrow phone width as well as a wide desktop.
