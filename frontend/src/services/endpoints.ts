/**
 * Every path this app asks the API for, written once.
 *
 * Paths only — no base URL, no fetching. `apiClient` turns a path into a request and is the only
 * thing that knows where the API lives, so a path can be reused by anything, including the
 * `EventSource` the live stream opens, which cannot go through `fetch` at all.
 *
 * Nothing outside this file should contain a string starting with `/api`. When the backend moves a
 * route, this is the file that changes.
 */

const root = '/api'
const matches = `${root}/matches`

/** Drops parameters that are not set, so an absent one produces no key rather than `=undefined`. */
function withQuery(path: string, params: Record<string, string | number | undefined>): string {
  const query = new URLSearchParams()

  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined) {
      query.set(key, String(value))
    }
  }

  const search = query.toString()
  return search ? `${path}?${search}` : path
}

export const endpoints = {
  matches: {
    live: () => `${matches}/live`,
    upcoming: () => `${matches}/upcoming`,

    /** Paged, because results accumulate rather than fitting in the provider's window. */
    recent: (page: number, pageSize: number) =>
      withQuery(`${matches}/recent`, { page, pageSize }),

    /** `idOrSlug` accepts either; the API resolves both. */
    details: (idOrSlug: string) => `${matches}/${encodeURIComponent(idOrSlug)}`,

    /** Server-Sent Events for one match. Opened with `EventSource`, not `fetch`. */
    stream: (idOrSlug: string) => `${matches}/${encodeURIComponent(idOrSlug)}/stream`,
  },
} as const
