import { Suspense } from 'react'
import { Outlet, useLocation } from 'react-router-dom'

import { ErrorBoundary } from '@/app/ErrorBoundary'
import { Skeleton } from '@/components/common/Skeleton'
import { Container } from '@/components/layout/Container'
import { Footer } from '@/components/layout/Footer'
import { Header } from '@/components/layout/Header'
import { OfflineNotice } from '@/components/layout/OfflineNotice'
import { RouteAnnouncer } from '@/components/layout/RouteAnnouncer'

export function AppLayout() {
  const { pathname } = useLocation()

  return (
    /* `dvh` rather than `svh`: the shell should fill whatever the viewport currently is, so the
       footer sits at the bottom without a gap when mobile browser chrome retracts. */
    <div className="flex min-h-dvh flex-col">
      {/*
        Ahead of the header so a keyboard or screen-reader user can reach the content without
        walking the whole nav on every page. Visible only when focused.

        `surface-raised` rather than `surface`, because this appears *over* the page: a translucent
        fill would leave whatever it covers legible through the one control someone is relying on
        to escape the nav.
      */}
      <a
        href="#main"
        className="sr-only focus-visible:not-sr-only focus-visible:absolute focus-visible:left-4 focus-visible:top-4 focus-visible:z-20 focus-visible:rounded-card focus-visible:bg-surface-raised focus-visible:px-4 focus-visible:py-2 focus-visible:text-sm focus-visible:font-medium focus-visible:text-brand-strong focus-visible:shadow-lift focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
      >
        Skip to content
      </a>

      <RouteAnnouncer />

      <Header />
      <OfflineNotice />

      {/*
        `tabIndex={-1}` so the skip link and the route announcer can both put focus here. It makes
        the landmark focusable programmatically without adding it to the tab order.
      */}
      <main id="main" tabIndex={-1} className="flex-1 py-6 focus:outline-none">
        {/* Keyed on the path so each navigation replays the entrance rather than swapping
            content in place, which otherwise reads as the page having failed to change. */}
        <Container key={pathname} className="animate-rise">
          {/*
            Suspense inside the boundary, so a chunk that fails to download is caught as an error
            rather than hanging on the fallback forever. The boundary itself needs no reset: the
            keyed container above remounts it on every navigation.
          */}
          <ErrorBoundary>
            <Suspense fallback={<PageFallback />}>
              <Outlet />
            </Suspense>
          </ErrorBoundary>
        </Container>
      </main>

      <Footer />
    </div>
  )
}

/**
 * Shown while a route's chunk is downloading.
 *
 * Shaped like a page rather than a spinner, so the layout does not jump when the real content
 * lands, and announced as busy so the wait is not silence to a screen reader.
 */
function PageFallback() {
  return (
    <div role="status" aria-busy="true" className="space-y-4">
      <span className="sr-only">Loading page</span>
      <Skeleton className="h-8 w-48 rounded-lg" />
      <Skeleton className="h-64 w-full rounded-xl" />
    </div>
  )
}
