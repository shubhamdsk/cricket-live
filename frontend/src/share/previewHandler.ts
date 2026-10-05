// `.js`, not `.ts`: this file also runs inside a Vercel Function as compiled JavaScript.
import { describeMatch, withPreview, type PreviewMatch } from './linkPreview.js'

type Fetch = (input: string, init?: RequestInit) => Promise<Response>

/**
 * Answers a link-preview bot's request for `/match/:slug`.
 *
 * `vercel.json` only routes preview bots here, so a failure costs a preview, never a page. Every
 * failure still answers with the site's own HTML, which carries the generic card: by the time a
 * request has been rewritten to this function there is nowhere to pass it on to.
 */
export async function previewResponse(
  request: Request,
  apiBaseUrl: string,
  fetchImpl: Fetch = fetch,
): Promise<Response> {
  const url = new URL(request.url)
  const slug = url.searchParams.get('slug') ?? ''
  const pageUrl = `${url.origin}/match/${slug}`

  let html: string
  try {
    const page = await fetchImpl(`${url.origin}/index.html`)
    if (!page.ok) return Response.redirect(`${url.origin}/`, 302)
    html = await page.text()
  } catch {
    return Response.redirect(`${url.origin}/`, 302)
  }

  try {
    const answer = await fetchImpl(`${apiBaseUrl}/api/matches/${encodeURIComponent(slug)}`, {
      headers: { Accept: 'application/json' },
      signal: AbortSignal.timeout(4000),
    })
    const envelope = (await answer.json()) as { success?: boolean; data?: PreviewMatch }

    if (answer.ok && envelope.success && envelope.data) {
      html = withPreview(html, { ...describeMatch(envelope.data), url: pageUrl })
    }
  } catch {
    // The API asleep or the match unknown: the generic card is the honest answer.
  }

  return new Response(html, {
    headers: {
      'content-type': 'text/html; charset=utf-8',
      'cache-control': 'public, max-age=60',
    },
  })
}
