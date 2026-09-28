import { Container } from '@/components/layout/Container'

export function Footer() {
  return (
    <footer className="border-t border-line bg-surface">
      <Container className="flex flex-col gap-1 py-6 text-sm text-ink-subtle sm:flex-row sm:items-center sm:justify-between">
        <p className="font-medium text-ink-muted">CricketLive</p>
        <p>Cricket data will be provided by SportScore.</p>
      </Container>
    </footer>
  )
}
