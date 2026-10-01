import type { Match } from '@/features/matches/types'

/**
 * Mirrors the series DTOs returned by our API. See docs/api.md.
 *
 * Two sources with two jobs behind these fields: the provider's index says which series exist,
 * and the matches we hold say what a page about one can actually show. That is why there are two
 * counts below and why they are not interchangeable.
 */

export interface Series {
  /** The provider's series id, shared by every match in it. */
  id: string
  /** Always ends in `id`, so a readable URL resolves without a lookup. */
  slug: string
  name: string
  /** When the earliest match we hold began, or when the series began if we hold none. */
  startTimeUtc: string
  /**
   * When the latest match we hold began, or `null` when we hold none.
   *
   * Never when the series ends: the provider sends `endDate` without a year, so we do not know.
   */
  lastMatchUtc: string | null
  /**
   * How many matches of this series we can show, not how many it contains.
   *
   * Our history starts when the archive did and the provider's window is a few days wide, so a
   * long tournament will report far fewer than it played. Compare `totalMatchCount`.
   */
  matchCount: number
  /**
   * How many matches the series has, or `null` when the provider's index did not cover it.
   *
   * `null` means "we were not told", never "none". Shown alongside `matchCount` so a small number
   * of held matches reads as a narrow window rather than as a short series.
   */
  totalMatchCount: number | null
  /** True when at least one match we hold has not finished. Always false for a series we hold none of. */
  isOngoing: boolean
}

/** One team's row in a points table, exactly as the source published it. */
export interface Standing {
  /** Such as "Elite Group A", or empty when the competition has a single table. */
  group: string
  /** As the table names it, usually an abbreviation like "MUM". Not expanded. */
  teamName: string
  played: number
  won: number
  lost: number
  tied: number
  noResult: number
  points: number
  /** Text, not a number: it is signed and published to three places, and is shown as given. */
  netRunRate: string
}

export interface SeriesDetails {
  series: Series
  /** Every match of this series we hold, in playing order. */
  matches: Match[]
  /**
   * Empty means no table was available, never that the series has no table. Those are different
   * claims, so the UI omits the section rather than rendering an empty one.
   */
  standings: Standing[]
}
