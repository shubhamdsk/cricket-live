// oxlint-disable react/only-export-components -- The lazy bindings below are component-shaped, so
// the rule reads this as a component file whose components are not exported. It is route
// configuration: the only export is the router, and the bindings exist to be referenced two lines
// later. The rule's real warning is that fast refresh is degraded for this file, which is true and
// costs nothing, because editing the route table is not an inner-loop activity.

import { createBrowserRouter } from 'react-router-dom'

import { lazyPage } from '@/app/lazyPage'
import { rewriteLegacyHashRoute } from '@/app/legacyHashRoute'
import { AppLayout } from '@/components/layout/AppLayout'
import { HomePage } from '@/pages/Home/HomePage'

/**
 * Every route but the home page is loaded on demand.
 *
 * Home is eager because it is the entry point: lazy-loading the first thing a visitor sees would
 * add a round trip before anything rendered, trading a smaller bundle for a slower first paint,
 * which is the wrong way round. Everything else is reached by a click, and a click is already an
 * acceptable moment to fetch.
 *
 * The suspense fallback lives in `AppLayout`, so the header and footer stay put while a page
 * arrives rather than the whole shell flashing.
 */
const LivePage = lazyPage(async () => (await import('@/pages/Live/LivePage')).LivePage)

const MatchesPage = lazyPage(
  async () => (await import('@/pages/Matches/MatchesPage')).MatchesPage,
)

const MatchDetailsPage = lazyPage(
  async () => (await import('@/pages/MatchDetails/MatchDetailsPage')).MatchDetailsPage,
)

const SeriesPage = lazyPage(async () => (await import('@/pages/Series/SeriesPage')).SeriesPage)

const SeriesDetailsPage = lazyPage(
  async () => (await import('@/pages/Series/SeriesDetailsPage')).SeriesDetailsPage,
)

const TeamsPage = lazyPage(async () => (await import('@/pages/Teams/TeamsPage')).TeamsPage)

const TeamDetailsPage = lazyPage(
  async () => (await import('@/pages/Teams/TeamDetailsPage')).TeamDetailsPage,
)

const SearchPage = lazyPage(async () => (await import('@/pages/Search/SearchPage')).SearchPage)

const NotFoundPage = lazyPage(
  async () => (await import('@/pages/NotFound/NotFoundPage')).NotFoundPage,
)

/**
 * Paths, not fragments.
 *
 * This used to be a hash router so that a deep link resolved without the host being configured to
 * rewrite unknown paths to `index.html` (D-019). The cost was that the site had exactly one
 * address a crawler could ever see — `/#/series` sends `/` to the server and keeps the rest to
 * itself — so no page but the home page could be indexed, no sitemap could list anything, and
 * per-page metadata was written for an audience that could not read it.
 *
 * The rewrite that hash routing existed to avoid is three lines of `vercel.json`, which was
 * already in the repository. D-038 records the reversal; `src/app/legacyHashRoute.ts` keeps links
 * that were shared in the old form working.
 */

// Here rather than in `main.tsx`, and the distinction is not stylistic. `createBrowserRouter` reads
// the current location as this module is evaluated, and a module body runs before the body of
// whatever imported it — so a rewrite in `main.tsx` would land after the router had already decided
// it was on `/`. Caught by opening /#/teams and getting the home page at the address /teams.
rewriteLegacyHashRoute()

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'live', element: <LivePage /> },
      { path: 'matches', element: <MatchesPage /> },
      { path: 'match/:slug', element: <MatchDetailsPage /> },
      { path: 'series', element: <SeriesPage /> },
      { path: 'series/:slug', element: <SeriesDetailsPage /> },
      { path: 'teams', element: <TeamsPage /> },
      { path: 'teams/:slug', element: <TeamDetailsPage /> },
      { path: 'search', element: <SearchPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
])
