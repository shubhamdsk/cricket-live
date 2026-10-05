import { describe, expect, it, vi } from 'vitest'

import { previewResponse } from '@/share/previewHandler'
import { match } from '@/test/fixtures'

const site = 'https://site.test'
const shell =
  '<html><head><title>Cricket Live</title>' +
  '<meta property="og:title" content="Cricket Live" /></head><body><div id="root"></div></body></html>'

function request(slug = 'india-vs-west-indies-m1') {
  return new Request(`${site}/api/preview?slug=${slug}`)
}

function fetching(api: () => Promise<Response>) {
  return vi.fn(async (input: string) =>
    input === `${site}/index.html` ? new Response(shell) : api(),
  )
}

describe('previewResponse', () => {
  it('names the match in the tags a link unfurler reads', async () => {
    const fetchImpl = fetching(async () =>
      Response.json({ success: true, message: 'Success', data: match() }),
    )

    const response = await previewResponse(request(), 'https://api.test', fetchImpl)
    const html = await response.text()

    expect(response.headers.get('content-type')).toContain('text/html')
    expect(html).toContain('content="IND 351/7 vs WI 352/5 · 3rd ODI | Cricket Live"')
    expect(fetchImpl).toHaveBeenCalledWith(
      'https://api.test/api/matches/india-vs-west-indies-m1',
      expect.anything(),
    )
  })

  it('serves the plain site when the API fails, so the preview is generic, not broken', async () => {
    const response = await previewResponse(
      request(),
      'https://api.test',
      fetching(() => Promise.reject(new TypeError('fetch failed'))),
    )

    expect(response.status).toBe(200)
    expect(await response.text()).toBe(shell)
  })

  it('serves the plain site for a match the API does not hold', async () => {
    const response = await previewResponse(
      request('nope'),
      'https://api.test',
      fetching(async () =>
        Response.json({ success: false, message: 'Match not found.' }, { status: 404 }),
      ),
    )

    expect(await response.text()).toBe(shell)
  })

  it('sends the bot home when even the site HTML cannot be read', async () => {
    const response = await previewResponse(
      request(),
      'https://api.test',
      vi.fn(() => Promise.reject(new TypeError('fetch failed'))),
    )

    expect(response.status).toBe(302)
    expect(response.headers.get('location')).toBe(`${site}/`)
  })
})
