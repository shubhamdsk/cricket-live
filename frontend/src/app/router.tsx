import { createHashRouter } from 'react-router-dom'

import { AppLayout } from '@/components/layout/AppLayout'
import { HomePage } from '@/pages/Home/HomePage'
import { LivePage } from '@/pages/Live/LivePage'
import { MatchDetailsPage } from '@/pages/MatchDetails/MatchDetailsPage'
import { MatchesPage } from '@/pages/Matches/MatchesPage'
import { NotFoundPage } from '@/pages/NotFound/NotFoundPage'
import { SearchPage } from '@/pages/Search/SearchPage'
import { SeriesDetailsPage } from '@/pages/Series/SeriesDetailsPage'
import { SeriesPage } from '@/pages/Series/SeriesPage'
import { TeamDetailsPage } from '@/pages/Teams/TeamDetailsPage'
import { TeamsPage } from '@/pages/Teams/TeamsPage'

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
