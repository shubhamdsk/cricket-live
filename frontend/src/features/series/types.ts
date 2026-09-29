import type { Match } from '@/features/matches/types'

/**
 * Mirrors the series DTOs returned by our API. See docs/api.md.
 *
 * A series here is assembled from the matches we hold rather than fetched as an entity, which is
 * why `matchCount` means what it says below and nothing more.
 */

export interface Series {
  /** The provider's series id, shared by every match in it. */
  id: string
  /** Always ends in `id`, so a readable URL resolves without a lookup. */
  slug: string
  name: string
  /** When the earliest match we hold began. */
  startTimeUtc: string
  /** When the latest match we hold began — not when the series ends, which we cannot know. */
  lastMatchUtc: string
  /**
   * How many matches of this series we can show, not how many it contains.
   *
   * Our history starts when the archive did and the provider's window is a few days wide, so a
   * long tournament will report far fewer than it played. The UI says "held" rather than implying
   * this is the full count.
   */
  matchCount: number
  /** True when at least one match we hold has not finished. */
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
