import type { Match, MatchFormat } from '@/features/matches/types'

/**
 * Mirrors the team DTOs returned by our API. See docs/api.md.
 *
 * As with a series, a team here is assembled from the matches we hold rather than fetched as an
 * entity — more so, in fact: the provider issues no team identifier and has no team endpoint, so
 * a team is only ever what its matches say about it.
 */

export interface Team {
  /** The slug, which is the team's only identifier. Derived from the name. */
  id: string
  name: string
  /** An abbreviation such as IND, or the full name when no match supplied one. */
  shortName: string
  /**
   * A path on our API, not a whole address, so it needs `apiUrl()` before it reaches an `<img>`.
   * Absent for most sides. The provider supplies crests only sometimes.
   */
  logoUrl: string | null
  /**
   * How many matches of this team we can show, not how many it has played.
   *
   * Same caveat as a series: history starts where the archive does. The UI says "held".
   */
  matchCount: number
  firstMatchUtc: string
  lastMatchUtc: string
  /** True when this team is in a match that has not finished. */
  isActive: boolean
}

/** A series this team appears in, with enough to link onward. */
export interface TeamSeries {
  id: string
  slug: string
  name: string
  matchCount: number
}

/**
 * A side this team has faced, and how often.
 *
 * Note what this is not: a head-to-head record. The provider states results as prose, so wins and
 * losses would have to be parsed out of a sentence, and a record assembled that way would be a
 * guess wearing the clothes of a statistic.
 */
export interface Opponent {
  id: string
  name: string
  matchCount: number
}

/** A format played, and how many times. Unlike a result, format is a mapped field. */
export interface FormatCount {
  format: MatchFormat
  matchCount: number
}

export interface TeamDetails {
  team: Team
  /** Every match we hold with this team on either side, in playing order. */
  matches: Match[]
  series: TeamSeries[]
  opponents: Opponent[]
  formats: FormatCount[]
}
