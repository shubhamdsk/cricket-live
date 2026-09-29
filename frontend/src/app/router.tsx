import { createHashRouter } from 'react-router-dom'

import { AppLayout } from '@/components/layout/AppLayout'
import { HomePage } from '@/pages/Home/HomePage'
import { LivePage } from '@/pages/Live/LivePage'
import { MatchDetailsPage } from '@/pages/MatchDetails/MatchDetailsPage'
import { MatchesPage } from '@/pages/Matches/MatchesPage'
import { NotFoundPage } from '@/pages/NotFound/NotFoundPage'


export const router = createHashRouter([
  {
    path: '/',
    element: <AppLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'live', element: <LivePage /> },
      { path: 'matches', element: <MatchesPage /> },
      { path: 'match/:slug', element: <MatchDetailsPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
])
