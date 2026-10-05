import { SpeedInsights } from '@vercel/speed-insights/react'
import { RouterProvider } from 'react-router-dom'

import { ErrorBoundary } from '@/app/ErrorBoundary'
import { AppProviders } from '@/app/providers'
import { router } from '@/app/router'

export default function App() {
  return (
    <AppProviders>
      <ErrorBoundary>
        <RouterProvider router={router} />
      </ErrorBoundary>
      {/* Served from the site's own /_vercel path in production, so the CSP needs no new host. */}
      <SpeedInsights />
    </AppProviders>
  )
}
