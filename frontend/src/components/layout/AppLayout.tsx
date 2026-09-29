import { Outlet, useLocation } from 'react-router-dom'

import { Container } from '@/components/layout/Container'
import { Footer } from '@/components/layout/Footer'
import { Header } from '@/components/layout/Header'

export function AppLayout() {
  const { pathname } = useLocation()

  return (
    /* `dvh` rather than `svh`: the shell should fill whatever the viewport currently is, so the
       footer sits at the bottom without a gap when mobile browser chrome retracts. */
    <div className="flex min-h-dvh flex-col">
      <Header />

      <main className="flex-1 py-6">
        {/* Keyed on the path so each navigation replays the entrance rather than swapping
            content in place, which otherwise reads as the page having failed to change. */}
        <Container key={pathname} className="animate-rise">
          <Outlet />
        </Container>
      </main>

      <Footer />
    </div>
  )
}
