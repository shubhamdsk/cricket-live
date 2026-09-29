import type { Match, MatchDetails } from '@/features/matches/types'
import { apiGet } from '@/services/apiClient'

/**
 * The three lists are partitions of a single upstream response, so asking for all of them costs
 * our API one provider call rather than three. Any of them can legitimately be empty.
 */

export function getLiveMatches(signal?: AbortSignal): Promise<Match[]> {
  return apiGet<Match[]>('/api/matches/live', signal)
}

export function getUpcomingMatches(signal?: AbortSignal): Promise<Match[]> {
  return apiGet<Match[]>('/api/matches/upcoming', signal)
}

export function getRecentMatches(signal?: AbortSignal): Promise<Match[]> {
  return apiGet<Match[]>('/api/matches/recent', signal)
}

/** Accepts the readable slug or the bare match id; the API resolves either. */
export function getMatchDetails(slug: string, signal?: AbortSignal): Promise<MatchDetails> {
  return apiGet<MatchDetails>(`/api/matches/${encodeURIComponent(slug)}`, signal)
}
