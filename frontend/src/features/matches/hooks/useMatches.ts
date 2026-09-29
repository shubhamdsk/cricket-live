import { useQuery } from '@tanstack/react-query'

import {
  getLiveMatches,
  getMatchDetails,
  getRecentMatches,
  getUpcomingMatches,
} from '@/features/matches/api/matchesApi'
import type { MatchDetails } from '@/features/matches/types'

/**
 * Refetch intervals are set against what the data can actually do, not against how live the page
 * feels. Our API caches a provider response for five minutes and the provider's own free data runs
 * a few minutes behind regardless, so polling faster than this buys nothing. Sprint 5 replaces the
 * interval on live data with an SSE push.
 */
const MINUTE = 60_000

export const matchKeys = {
  all: ['matches'] as const,
  live: () => [...matchKeys.all, 'live'] as const,
  upcoming: () => [...matchKeys.all, 'upcoming'] as const,
  recent: () => [...matchKeys.all, 'recent'] as const,
  details: (slug: string) => [...matchKeys.all, 'details', slug] as const,
}

export function useLiveMatches() {
  return useQuery({
    queryKey: matchKeys.live(),
    queryFn: ({ signal }) => getLiveMatches(signal),
    staleTime: MINUTE,
    refetchInterval: MINUTE,
    // Coming back to the tab is the moment a stale score is most obvious.
    refetchOnWindowFocus: true,
  })
}

export function useUpcomingMatches() {
  return useQuery({
    queryKey: matchKeys.upcoming(),
    queryFn: ({ signal }) => getUpcomingMatches(signal),
    // A fixture list changes when a match starts, not minute to minute.
    staleTime: 5 * MINUTE,
  })
}

export function useRecentMatches() {
  return useQuery({
    queryKey: matchKeys.recent(),
    queryFn: ({ signal }) => getRecentMatches(signal),
    staleTime: 5 * MINUTE,
  })
}

export function useMatchDetails(slug: string | undefined) {
  return useQuery({
    queryKey: matchKeys.details(slug ?? ''),
    queryFn: ({ signal }) => getMatchDetails(slug!, signal),
    enabled: Boolean(slug),
    staleTime: MINUTE,
    // A finished match cannot change, so stop asking.
    refetchInterval: (query) => {
      const match = query.state.data as MatchDetails | undefined
      return match?.status === 'live' ? MINUTE : false
    },
    refetchOnWindowFocus: true,
  })
}
