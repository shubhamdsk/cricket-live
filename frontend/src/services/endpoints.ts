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
const series = `${root}/series`
const teams = `${root}/teams`
const search = `${root}/search`

type QueryValue = string | number | undefined

/** Drops parameters that are not set, so an absent one produces no key rather than `=undefined`. */
function withQuery(path: string, params: Record<string, QueryValue>): string {
  const query = new URLSearchParams()

  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') {
      query.set(key, String(value))
    }
  }

  const search = query.toString()
  return search ? `${path}?${search}` : path
}

/**
 * The filter, as the API spells it.
 *
 * `from` and `to` are ISO instants rather than dates. A calendar day is a different interval in
 * every timezone and the server cannot know which one the reader meant, so converting a local day
 * into a range is this side's job.
 */
export interface MatchFilterParams {
  status?: 'live' | 'upcoming' | 'completed'
  from?: string
  to?: string
  series?: string
}

export const endpoints = {
  matches: {
    live: (filter: MatchFilterParams = {}) => withQuery(`${matches}/live`, { ...filter }),
    upcoming: (filter: MatchFilterParams = {}) =>
      withQuery(`${matches}/upcoming`, { ...filter }),

    /** Paged, because results accumulate rather than fitting in the provider's window. */
    recent: (page: number, pageSize: number, filter: MatchFilterParams = {}) =>
      withQuery(`${matches}/recent`, { ...filter, page, pageSize }),

    /** The series that have a match behind them. Never a fixed list. */
    series: () => `${matches}/series`,

    /** `idOrSlug` accepts either; the API resolves both. */
    details: (idOrSlug: string) => `${matches}/${encodeURIComponent(idOrSlug)}`,

    /** Server-Sent Events for one match. Opened with `EventSource`, not `fetch`. */
    stream: (idOrSlug: string) => `${matches}/${encodeURIComponent(idOrSlug)}/stream`,
  },

  series: {
    /** Every series we hold a match of, assembled from those matches rather than fetched. */
    all: () => series,

    /** `idOrSlug` accepts either; the API resolves both. */
    details: (idOrSlug: string) => `${series}/${encodeURIComponent(idOrSlug)}`,
  },

  teams: {
    /** Every team we hold a match of. There is no team endpoint upstream to call instead. */
    all: () => teams,

    /** A slug such as `india`. Teams have no provider id, so the slug is the identifier. */
    details: (slug: string) => `${teams}/${encodeURIComponent(slug)}`,
  },

  /** Matches, teams and series whose names contain the term. Two characters minimum. */
  search: (term: string) => withQuery(search, { q: term }),
} as const
