import { useSyncExternalStore } from 'react'

import { Container } from '@/components/layout/Container'

function subscribe(onChange: () => void) {
  window.addEventListener('online', onChange)
  window.addEventListener('offline', onChange)
  return () => {
    window.removeEventListener('online', onChange)
    window.removeEventListener('offline', onChange)
  }
}

/**
 * Said once, above every page, because offline the service worker answers with whatever this
 * device saw last, and a match page has no capture time of its own to show.
 */
export function OfflineNotice() {
  const online = useSyncExternalStore(
    subscribe,
    () => navigator.onLine,
    () => true,
  )

  if (online) return null

  return (
    <div role="status" className="border-b border-warn-line bg-warn-soft">
      <Container className="py-2 text-sm text-ink-muted">
        You&rsquo;re offline. Scores shown are the last ones this device loaded.
      </Container>
    </div>
  )
}
