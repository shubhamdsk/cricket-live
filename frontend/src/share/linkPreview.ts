/**
 * Per-match link previews for apps that read a page without running it.
 *
 * WhatsApp, X, Slack and the rest fetch the HTML and read its meta tags, so a shared match link
 * shows the site's generic card unless the tags already name the match. `middleware.ts` uses this
 * to rewrite them for those readers only; people get the ordinary app.
 *
 * Self-contained, with its own copy of the few fields it reads, because the middleware is bundled
 * separately from the app and cannot resolve the app's `@/` imports.
 */

interface PreviewSide {
  team: { name: string; shortName: string }
  innings: { runs: number; wickets: number; overs: string }[]
}

export interface PreviewMatch {
  status: 'live' | 'upcoming' | 'completed'
  matchTitle: string
  seriesName: string
  venue: string
  startTimeUtc: string
  statusText: string
  home: PreviewSide
  away: PreviewSide
}

export interface Preview {
  title: string
  description: string
  url: string
}

const previewers =
  /bot|crawler|spider|facebookexternalhit|whatsapp|slack|discord|telegram|linkedin|embedly|pinterest|skypeuripreview|vkshare|applebot|quora/i

export function isLinkPreviewBot(userAgent: string | null): boolean {
  return userAgent !== null && previewers.test(userAgent)
}

const startDate = new Intl.DateTimeFormat('en-GB', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
})

function score(side: PreviewSide): string {
  return side.innings
    .map(({ runs, wickets }) => (wickets === 10 ? `${runs}` : `${runs}/${wickets}`))
    .join(' & ')
}

function sideLabel(side: PreviewSide): string {
  const runs = score(side)
  return runs ? `${side.team.shortName} ${runs}` : side.team.shortName
}

/** "IND 351/7 vs WI 352/5 · 3rd ODI", with the result or start date underneath. */
export function describeMatch(match: PreviewMatch): Omit<Preview, 'url'> {
  const title = `${sideLabel(match.home)} vs ${sideLabel(match.away)} · ${match.matchTitle}`

  const lead =
    match.status === 'upcoming'
      ? `Starts ${startDate.format(new Date(match.startTimeUtc))}`
      : match.status === 'live'
        ? `Live: ${match.statusText || 'in progress'}`
        : match.statusText || 'Result'

  const where = [match.seriesName, match.venue].filter(Boolean).join(', ')

  return {
    title,
    description: `${lead}. ${match.home.team.name} vs ${match.away.team.name}${where ? `, ${where}` : ''}.`,
  }
}

function escape(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('"', '&quot;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
}

function setMeta(html: string, attribute: 'property' | 'name', key: string, value: string) {
  const tag = new RegExp(`(<meta\\s+${attribute}="${key}"\\s+content=")[^"]*(")`)
  return html.replace(tag, `$1${escape(value)}$2`)
}

/** The site's HTML with its title, description and address replaced by the match's. */
export function withPreview(html: string, { title, description, url }: Preview): string {
  const fullTitle = `${title} | Cricket Live`
  let page = html.replace(/<title>[^<]*<\/title>/, `<title>${escape(fullTitle)}</title>`)

  page = setMeta(page, 'name', 'description', description)
  page = setMeta(page, 'property', 'og:type', 'article')
  page = setMeta(page, 'property', 'og:title', fullTitle)
  page = setMeta(page, 'property', 'og:description', description)
  page = setMeta(page, 'property', 'og:url', url)
  page = setMeta(page, 'name', 'twitter:title', fullTitle)
  page = setMeta(page, 'name', 'twitter:description', description)

  return page.replace(/(<link rel="canonical" href=")[^"]*(")/, `$1${escape(url)}$2`)
}
