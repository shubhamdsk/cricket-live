import { useEffect, useRef } from 'react'
import { useLocation } from 'react-router-dom'

import { useUiStore } from '@/store/uiStore'

/**
 * Tells a screen reader that the page changed, and moves focus so the keyboard follows.
 *
 * Both halves fix the same defect, which is that a client-side navigation is not a page load. The
 * browser does none of what it normally would: the title is not read, focus stays on whatever link
 * was clicked, and to a screen-reader user the app appears not to have responded at all.
 */
export function RouteAnnouncer() {
  const { pathname } = useLocation()
  const announcement = useUiStore((state) => state.announcement)
  const target = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const main = document.getElementById('main')
    if (main === null) return

    // `preventScroll`, because the container is already scrolled to the top by the navigation and
    // letting focus scroll as well fights it. Focus goes to the landmark rather than the first
    // heading so that the next Tab lands on the page's own first control, not on something after
    // whatever heading happened to be first.
    main.focus({ preventScroll: true })
  }, [pathname])

  return (
    /*
      `polite` so it waits for the reader to finish whatever it was saying, and `atomic` so the
      whole message is read rather than only the words that changed. Visually hidden rather than
      `display: none`, which would take it out of the accessibility tree and defeat the point.
    */
    <div ref={target} aria-live="polite" aria-atomic="true" className="sr-only">
      {announcement === '' ? '' : `${announcement}, page loaded`}
    </div>
  )
}
