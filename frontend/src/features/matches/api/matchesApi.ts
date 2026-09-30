import type { Scorecard } from '@/features/matches/scorecardTypes'
import type { Match, MatchDetails, Paged } from '@/features/matches/types'
import { ApiError, apiGet, apiUrl } from '@/services/apiClient'
import type { MatchFilterParams } from '@/services/endpoints'
import { endpoints } from '@/services/endpoints'

/**
 * Live and upcoming are partitions of a single upstream response, so asking for both costs our API
 * one provider call rather than two. Either can legitimately be empty.
 *
 * Results are different: they come from what the API kept rather than from the provider's short
 * window, so the list has no natural end and is the one list that is paged.
 */

export function getLiveMatches(
  filter: MatchFilterParams = {},
  signal?: AbortSignal,
): Promise<Match[]> {
  return apiGet<Match[]>(endpoints.matches.live(filter), signal)
}

export function getUpcomingMatches(
  filter: MatchFilterParams = {},
  signal?: AbortSignal,
): Promise<Match[]> {
  return apiGet<Match[]>(endpoints.matches.upcoming(filter), signal)
}

export function getRecentMatches(
  page: number,
  pageSize: number,
  filter: MatchFilterParams = {},
  signal?: AbortSignal,
): Promise<Paged<Match>> {
  return apiGet<Paged<Match>>(endpoints.matches.recent(page, pageSize, filter), signal)
}

/** The series that can be filtered to, read from the matches that exist. */
export function getSeriesNames(signal?: AbortSignal): Promise<string[]> {
  return apiGet<string[]>(endpoints.matches.series(), signal)
}

/** Accepts the readable slug or the bare match id; the API resolves either. */
export function getMatchDetails(slug: string, signal?: AbortSignal): Promise<MatchDetails> {
  return apiGet<MatchDetails>(endpoints.matches.details(slug), signal)
}

/**
 * The full card, or null when there is none to be had.
 *
 * A 404 here is the ordinary case rather than a failure: the scorecard source is off by default,
 * does not carry every match, and has a request allowance it can exhaust. All three arrive as a
 * 404 and all three mean the same thing to a reader, so they are turned into null and the section
 * is simply not rendered. Every other status still throws, because a scorecard endpoint returning
 * a 500 is a real problem and hiding it would make it invisible.
 */
export async function getMatchScorecard(
  slug: string,
  signal?: AbortSignal,
): Promise<Scorecard | null> {
  try {
    return await apiGet<Scorecard>(endpoints.matches.scorecard(slug), signal)
  } catch (cause) {
    if (cause instanceof ApiError && cause.status === 404) {
      return null
    }

    throw cause
  }
}

/**
 * Returns a URL rather than a promise because the live stream is opened by `EventSource`, which
 * does its own requesting. It belongs in this file regardless: reaching the matches API is what
 * this file is for, and the alternative is a second place that builds an API URL by hand.
 */
export function matchStreamUrl(slug: string): string {
  return apiUrl(endpoints.matches.stream(slug))
}
