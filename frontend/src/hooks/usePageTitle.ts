import { useEffect } from 'react'

import { useUiStore } from '@/store/uiStore'

const SUFFIX = 'Cricket Live'

/**
 * Names the page, in the title bar and out loud.
 *
 * This is the one piece of per-page metadata worth maintaining under hash routing. Crawlers and
 * link unfurlers only ever see the path, never the fragment (D-019), so per-page `<meta>` tags
 * would be written for an audience that cannot read them. The document title has a different
 * audience entirely: browser tabs, bookmarks, history entries, and screen readers, for whom the
 * title is the primary way of knowing which page you have landed on.
 *
 * Pass `null` while the name is still loading rather than a placeholder, so the title does not
 * flicker through "Loading" on the way to the real thing.
 */
export function usePageTitle(name: string | null) {
  const announce = useUiStore((state) => state.announce)

  useEffect(() => {
    if (name === null) return

    document.title = `${name} \u00b7 ${SUFFIX}`

    // Changing the title is not itself an announcement: a screen reader reads the title when a
    // document loads, and a client-side navigation is not a load. Hence the live region.
    announce(name)
  }, [name, announce])

  // Restoring on unmount, so a page that leaves before its name arrives does not hand its title
  // to whatever comes next.
  useEffect(
    () => () => {
      document.title = SUFFIX
    },
    [],
  )
}
