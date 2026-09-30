// oxlint-disable react/only-export-components -- The lazy bindings below are component-shaped, so
// the rule reads this as a component file whose components are not exported. It is route
// configuration: the only export is the router, and the bindings exist to be referenced two lines
// later. The rule's real warning is that fast refresh is degraded for this file, which is true and
// costs nothing, because editing the route table is not an inner-loop activity.

import { lazy } from 'react'
import { createHashRouter } from 'react-router-dom'

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
const LivePage = lazy(async () => ({
  default: (await import('@/pages/Live/LivePage')).LivePage,
}))

const MatchesPage = lazy(async () => ({
  default: (await import('@/pages/Matches/MatchesPage')).MatchesPage,
}))

const MatchDetailsPage = lazy(async () => ({
  default: (await import('@/pages/MatchDetails/MatchDetailsPage')).MatchDetailsPage,
}))

const SeriesPage = lazy(async () => ({
  default: (await import('@/pages/Series/SeriesPage')).SeriesPage,
}))

const SeriesDetailsPage = lazy(async () => ({
  default: (await import('@/pages/Series/SeriesDetailsPage')).SeriesDetailsPage,
}))

const TeamsPage = lazy(async () => ({
  default: (await import('@/pages/Teams/TeamsPage')).TeamsPage,
}))

const TeamDetailsPage = lazy(async () => ({
  default: (await import('@/pages/Teams/TeamDetailsPage')).TeamDetailsPage,
}))

const SearchPage = lazy(async () => ({
  default: (await import('@/pages/Search/SearchPage')).SearchPage,
}))

const NotFoundPage = lazy(async () => ({
  default: (await import('@/pages/NotFound/NotFoundPage')).NotFoundPage,
}))

/**
 * Hash routing, so a deep link resolves without the host being configured to rewrite unknown
 * paths to `index.html`. Reasoning and its cost are in docs/decisions.md, D-019.
 */
export const router = createHashRouter([
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
