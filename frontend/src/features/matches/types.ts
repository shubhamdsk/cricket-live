/**
 * Mirrors the DTOs returned by our API. See docs/api.md.
 *
 * These began in Sprint 2 as a contract written from what the screens needed. Sprint 3 measured a
 * real provider and most of it held; what did not is recorded in docs/decisions.md D-012.
 */

export type MatchStatus = 'live' | 'upcoming' | 'completed'

/** `OTHER` covers formats the provider carries but we do not model, such as T10. */
export type MatchFormat = 'T20' | 'ODI' | 'TEST' | 'OTHER'

export interface TeamSummary {
  id: string
  name: string
  shortName: string
  /** Null for teams the provider holds no profile for, which is most domestic sides. */
  logoUrl: string | null
}

export interface InningsScore {
  number: number
  runs: number
  wickets: number
  /** Cricket over notation: `12.3` is twelve overs and three balls. Never arithmetic. */
  overs: string
}

/** A side and every innings it has batted. Tests hold two; a side yet to bat holds none. */
export interface TeamInnings {
  team: TeamSummary
  innings: InningsScore[]
}

export interface Match {
  id: string
  /** Always ends in `id`, so a readable URL resolves without a lookup. */
  slug: string
  status: MatchStatus
  format: MatchFormat
  seriesName: string
  matchTitle: string
  venue: string
  startTimeUtc: string
  home: TeamInnings
  away: TeamInnings
  /** The provider's own sentence, such as "India won by 8 wkts". Displayed verbatim. */
  statusText: string
}

/**
 * One page of a list the API does not return whole.
 *
 * `total` is what the archive holds, so it grows as matches finish and is not a fixed number the
 * UI can plan around. `hasMore` is the API's own answer rather than something derived here.
 */
export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
  hasMore: boolean
}

export interface Batter {
  name: string
  runs: number
  /** Balls faced. Whole deliveries, unlike overs, so arithmetic on it is safe. */
  balls: number
}

export interface MatchDetails extends Match {
  /** What the provider claims to hold, not what we display. False everywhere so far. */
  hasBallByBall: boolean
  hasSquads: boolean
  /**
   * Batters at the crease, from a supplementary source that is off by default and only knows
   * matches somebody mapped by hand. Empty means "we do not know", never "nobody is batting",
   * so the UI omits the section rather than claiming the crease is empty.
   */
  currentBatters: Batter[]
}
