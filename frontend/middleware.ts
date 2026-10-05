import { next } from '@vercel/functions'

import {
  describeMatch,
  isLinkPreviewBot,
  withPreview,
  type PreviewMatch,
} from './src/share/linkPreview.ts'

export const config = { matcher: '/match/:slug*' }

const api = process.env.VITE_API_BASE_URL ?? 'https://cricket-live-api-qwo6.onrender.com'

/**
 * Gives link-preview bots a page whose meta tags describe the shared match.
 *
 * Everyone else passes straight through to the app. Any failure — the API asleep, the match
 * unknown — also passes through, so the worst case is the generic card the site always had.
 * One API read per preview, which the API answers from its cache or archive in almost every case.
 */
export default async function middleware(request: Request) {
  if (!isLinkPreviewBot(request.headers.get('user-agent'))) return next()

  const url = new URL(request.url)
  const slug = url.pathname.split('/')[2]
  if (!slug) return next()

  try {
    const [page, answer] = await Promise.all([
      fetch(new URL('/index.html', url)),
      fetch(`${api}/api/matches/${encodeURIComponent(slug)}`, {
        headers: { Accept: 'application/json' },
        signal: AbortSignal.timeout(4000),
      }),
    ])
    if (!page.ok || !answer.ok) return next()

    const envelope = (await answer.json()) as { success?: boolean; data?: PreviewMatch }
    if (!envelope.success || !envelope.data) return next()

    const html = withPreview(await page.text(), {
      ...describeMatch(envelope.data),
      url: `${url.origin}${url.pathname}`,
    })

    return new Response(html, {
      headers: {
        'content-type': 'text/html; charset=utf-8',
        'cache-control': 'public, max-age=60',
      },
    })
  } catch {
    return next()
  }
}
