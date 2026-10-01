/**
 * Facts about the site as a whole, in one place because they are repeated in `index.html`, which
 * cannot import anything, and a canonical URL that disagrees with itself is worse than none.
 */

/**
 * The one origin the site is indexed under.
 *
 * It is a constant rather than `window.location.origin` on purpose. The app is reachable at more
 * than one Vercel address, and a canonical built from wherever the reader happens to be would tell
 * a search engine that each address is the original — which is the opposite of what the tag is for.
 * Naming a fixed origin means the other addresses point here instead of competing.
 *
 * If this ever gains a custom domain, this line and the absolute URLs in `index.html` change
 * together.
 */
export const SITE_ORIGIN = 'https://cricket-live-shubhamdsk1.vercel.app'

export const SITE_NAME = 'Cricket Live'

export const SITE_DESCRIPTION =
  'Live cricket scores, fixtures, results and series standings, updated while play continues.'
