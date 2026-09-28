import { Outlet } from 'react-router-dom'

import { Container } from '@/components/layout/Container'
import { Footer } from '@/components/layout/Footer'
import { Header } from '@/components/layout/Header'

export function AppLayout() {
  return (
    <div className="flex min-h-svh flex-col">
      <Header />

      <main className="flex-1 py-6">
        <Container>
          <Outlet />
        </Container>
      </main>

      <Footer />
    </div>
  )
}
