import { useEffect } from 'react'

import { SITE_DESCRIPTION, SITE_NAME, SITE_ORIGIN } from '@/app/site'
import { useUiStore } from '@/store/uiStore'

export interface PageMeta {
  /** Overrides the site description for this page. Keep it under about 160 characters. */
  description?: string

  /**
   * Asks search engines not to index this page.
   *
   * For a page that is real but not worth a result of its own: a search results page, whose content
   * is a rearrangement of pages that already exist, and the not-found page, which a static host
   * serves with a 200 and would otherwise be indexed as if it were content.
   */
  noindex?: boolean
}

/**
 * Names and describes the page, for readers and for crawlers.
 *
 * Until D-038 this only set the document title, and the reason was sound at the time: routes lived
 * in the URL fragment, a crawler is never sent the fragment, so every page shared one address and
 * per-page metadata was written for an audience that could not read it. Now that routes are paths,
 * each page has an address of its own and there is something to describe.
 *
 * Pass `null` as the name while it is still loading rather than a placeholder, so the title does
 * not flicker through "Loading" on the way to the real thing.
 *
 * **What this does not fix.** The tags are written by JavaScript into a static document. Search
 * engines that render pages will see them; link unfurlers — the ones behind a pasted link in a
 * chat window — generally do not run scripts, so they keep reading the static tags in `index.html`.
 * Those are therefore written to describe the site rather than left empty. Fixing that properly
 * means pre-rendering, which is a different piece of work and is recorded as such in D-038.
 */
export function usePageMeta(name: string | null, meta: PageMeta = {}) {
  const announce = useUiStore((state) => state.announce)
  const { description, noindex } = meta

  useEffect(() => {
    if (name === null) return

    const title = `${name} \u00b7 ${SITE_NAME}`

    document.title = title

    // Every page sets all of these every time, rather than only the ones it cares about. A hook
    // that left a tag alone would leave the previous page's claim in place, which on a
    // client-side navigation means the description of wherever the reader came from.
    setMeta('name', 'description', description ?? SITE_DESCRIPTION)
    setMeta('property', 'og:title', title)
    setMeta('property', 'og:description', description ?? SITE_DESCRIPTION)
    setMeta('name', 'twitter:title', title)
    setMeta('name', 'twitter:description', description ?? SITE_DESCRIPTION)

    // Built from the configured origin rather than the current one, for the reason given in
    // src/app/site.ts, and deliberately without the query string: two searches are not two pages,
    // and a canonical carrying the query would invite every term anyone searched to be indexed.
    const canonical = `${SITE_ORIGIN}${window.location.pathname}`

    setLink('canonical', canonical)
    setMeta('property', 'og:url', canonical)

    // Removed rather than set to a value meaning "do index", because no such value is needed: the
    // absence of the tag is the default. Set explicitly either way, so navigating off a noindexed
    // page does not leave the instruction behind on the next one.
    if (noindex) {
      setMeta('name', 'robots', 'noindex, follow')
    } else {
      document.head.querySelector('meta[name="robots"]')?.remove()
    }

    // Changing the title is not itself an announcement: a screen reader reads the title when a
    // document loads, and a client-side navigation is not a load. Hence the live region.
    announce(name)
  }, [name, description, noindex, announce])

  // Restoring on unmount, so a page that leaves before its name arrives does not hand its title
  // to whatever comes next.
  useEffect(
    () => () => {
      document.title = SITE_NAME
    },
    [],
  )
}

/**
 * Writes a `<meta>` tag, creating it if the document does not already carry one.
 *
 * `index.html` declares the site-level tags with `name` on some and `property` on others, because
 * that is what each specification asks for — Open Graph uses `property`, everything else uses
 * `name`. Matching on the wrong attribute would silently append a duplicate rather than update,
 * and a document with two `og:title` tags is read by different consumers differently.
 */
function setMeta(attribute: 'name' | 'property', key: string, content: string): void {
  const selector = `meta[${attribute}="${key}"]`

  let tag = document.head.querySelector<HTMLMetaElement>(selector)

  if (tag === null) {
    tag = document.createElement('meta')
    tag.setAttribute(attribute, key)
    document.head.appendChild(tag)
  }

  tag.content = content
}

function setLink(rel: string, href: string): void {
  let tag = document.head.querySelector<HTMLLinkElement>(`link[rel="${rel}"]`)

  if (tag === null) {
    tag = document.createElement('link')
    tag.rel = rel
    document.head.appendChild(tag)
  }

  tag.href = href
}
